using Microsoft.Extensions.DependencyInjection;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Services;

namespace SubastaYa.Application;

/// <summary>Registra los casos de uso de la capa de aplicación en el contenedor de dependencias.</summary>
public static class ApplicationServiceRegistration
{
    public static IServiceCollection AddApplicationLayer(this IServiceCollection services)
    {
        services.AddScoped<ICatalogService, CatalogService>();

        return services;
    }
}
