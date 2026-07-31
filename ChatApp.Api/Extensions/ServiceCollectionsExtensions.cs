using ChatApp.Api.Data;
using ChatApp.Api.Services;
using ChatApp.Api.Services.Abstractions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.Text;

namespace ChatApp.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static WebApplicationBuilder AddSwagger(this WebApplicationBuilder builder)
    {
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "ChatApp API",
                Version = "v1"
            });

            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                In = ParameterLocation.Header,
                Description = "Вставь access-токен без слова 'Bearer' — Swagger добавит его сам"
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference("Bearer", document)] = new List<string>()
            });
        });

        return builder;
    }

    public static WebApplicationBuilder AddData(this WebApplicationBuilder builder)
    {
        builder.Services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));

        return builder;
    }

    public static WebApplicationBuilder AddBearerAuthentication(this WebApplicationBuilder builder)
    {
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = builder.Configuration["Jwt:Issuer"],
                    ValidAudience = builder.Configuration["Jwt:Audience"],
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]!)),

                    // По умолчанию есть допуск в 5 минут на рассинхрон часов между серверами —
                    // для access-токена с 15-минутным временем жизни это ощутимая доля, обнуляем.
                    ClockSkew = TimeSpan.Zero
                };

                // SignalR не может передать заголовок Authorization в WebSocket-хендшейке
                // из браузера — токен приходит через query string (?access_token=...).
                // Перехватываем его здесь и подставляем как context.Token, ТОЛЬКО для
                // путей хаба — на обычные REST-запросы это не должно влиять, там токен
                // и так приходит нормальным заголовком Authorization.
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];
                        var path = context.HttpContext.Request.Path;

                        if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs/chat"))
                            context.Token = accessToken;

                        return Task.CompletedTask;
                    }
                };
            });

        builder.Services.AddAuthorization();

        return builder;
    }

    public static WebApplicationBuilder AddApplicationServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<ITokenService, TokenService>();
        builder.Services.AddScoped<IMessageService, MessageService>();

        // Singleton — трекер соединений должен жить одним общим экземпляром на всё
        // приложение (в памяти процесса), а не создаваться заново на каждый запрос,
        // как это было бы со Scoped.
        builder.Services.AddSingleton<IConnectionTracker, InMemoryConnectionTracker>();

        return builder;
    }

    public static WebApplicationBuilder AddSignalRServices(this WebApplicationBuilder builder)
    {
        builder.Services.AddSignalR();

        // TODO (Этап масштабирования): при переходе на несколько инстансов приложения
        // сюда добавляется Redis backplane:
        // builder.Services.AddSignalR().AddStackExchangeRedis(builder.Configuration.GetConnectionString("Redis"));
        // и InMemoryConnectionTracker выше меняется на Redis-based реализацию —
        // иначе группы и presence не будут синхронизированы между инстансами.

        return builder;
    }
}