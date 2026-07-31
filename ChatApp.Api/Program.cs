using ChatApp.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

// TODO (Этап 4-5): SignalR + Redis backplane, CORS, health checks для готовности к продакшену.

builder
    .AddData()
    .AddApplicationServices()
    .AddBearerAuthentication()
    .AddSignalRServices()
    .AddSwagger();

builder.Services.AddControllers();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(); // доступно на /swagger
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "ok" }));

app.Run();