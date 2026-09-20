using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options => options.SwaggerDoc("v1", new OpenApiInfo
{
    Title = "SubastaYa API",
    Version = "v1",
    Description = "API REST de la plataforma de subastas en tiempo real SubastaYa."
}));

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI(options =>
{
    options.SwaggerEndpoint("/swagger/v1/swagger.json", "SubastaYa API v1");
    options.RoutePrefix = "swagger";
});

app.MapControllers();

app.Run();
