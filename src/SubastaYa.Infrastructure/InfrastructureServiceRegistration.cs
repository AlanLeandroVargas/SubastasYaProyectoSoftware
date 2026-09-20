using Microsoft.Extensions.DependencyInjection;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Infrastructure.Persistence.InMemory;
using SubastaYa.Infrastructure.Time;

namespace SubastaYa.Infrastructure;

/// <summary>Registra las implementaciones concretas de los puertos declarados por la aplicación.</summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureLayer(this IServiceCollection services)
    {
        services.AddSingleton<IClock, SystemClock>();

        // Persistencia provisional en memoria. Se reemplaza por Entity Framework Core sin que
        // la capa de aplicación se entere: sólo cambia el registro de estos dos puertos.
        services.AddSingleton<InMemoryCatalogStore>();
        services.AddScoped<IAuctionRepository, InMemoryAuctionRepository>();
        services.AddScoped<ICategoryRepository, InMemoryCategoryRepository>();

        return services;
    }
}
