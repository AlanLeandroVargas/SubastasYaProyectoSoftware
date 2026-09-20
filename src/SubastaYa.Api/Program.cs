using SubastaYa.Api.Configuration;
using SubastaYa.Api.Hubs;
using SubastaYa.Api.Middleware;
using SubastaYa.Application;
using SubastaYa.Infrastructure;
using SubastaYa.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Composición de las capas: presentación -> aplicación -> infraestructura.
builder.Services.AddApplicationLayer();
builder.Services.AddInfrastructureLayer(builder.Configuration);
builder.Services.AddWebLayer(builder.Configuration);

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

app.UseCors(WebServiceRegistration.CorsPolicyName);
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<AuctionHub>(AuctionHub.Path);

app.Run();
