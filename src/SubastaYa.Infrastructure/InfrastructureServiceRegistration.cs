using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Infrastructure.Persistence;
using SubastaYa.Infrastructure.Persistence.Repositories;
using SubastaYa.Infrastructure.Persistence.Seeding;
using SubastaYa.Infrastructure.Time;

namespace SubastaYa.Infrastructure;

/// <summary>Registra las implementaciones concretas de los puertos declarados por la aplicación.</summary>
public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureLayer(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddDbContext<SubastaYaDbContext>(options =>
            // No se habilita el reintento automático de conexión: es incompatible con las
            // transacciones iniciadas manualmente, que serán el núcleo del manejo de garantías.
            options.UseSqlServer(
                configuration.GetConnectionString("SubastaYa"),
                sqlServer => sqlServer.MigrationsAssembly(typeof(SubastaYaDbContext).Assembly.FullName)));

        services.AddSingleton<IClock, SystemClock>();

        services.AddScoped<IAuctionRepository, AuctionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
