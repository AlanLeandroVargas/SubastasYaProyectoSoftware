using System.Text.Json;
using Microsoft.OpenApi.Models;
using SubastaYa.Api.Middleware;
using SubastaYa.Application;
using SubastaYa.Infrastructure;
using SubastaYa.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Composición de las capas: presentación -> aplicación -> infraestructura.
builder.Services.AddApplicationLayer();
builder.Services.AddInfrastructureLayer(builder.Configuration);

builder.Services
    .AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "SubastaYa API",
    Version = "v1",
    Description = "API REST de la plataforma de subastas en tiempo real SubastaYa."
}));

var app = builder.Build();

// Migraciones y datos semilla antes de aceptar tráfico.
await app.Services.PrepareDatabaseAsync();

app.UseMiddleware<GlobalExceptionMiddleware>();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SubastaYa API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

app.Run();
