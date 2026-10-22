using ChatApp.Api.Extensions;
using ChatApp.Api.Hubs;

var builder = WebApplication.CreateBuilder(args);

// TODO (Этап 4-5): SignalR + Redis backplane, CORS, health checks для готовности к продакшену.

builder
    .AddData()
    .AddApplicationServices()
    .AddBearerAuthentication()
    .AddSignalRServices()
    .AddCorsPolicy()
    .AddSwagger();

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(); // доступно на /swagger
}

// CORS обязан стоять до аутентификации: preflight-запрос (OPTIONS) приходит без
// токена, и если до него дойдёт [Authorize], браузер получит 401 вместо CORS-заголовков
app.UseCors();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));
app.MapHub<ChatHub>("/hubs/chat");

app.Run();