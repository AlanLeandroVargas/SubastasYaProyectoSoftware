using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.RealTime;
using SubastaYa.Application.Abstractions.Security;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Infrastructure.Persistence;
using SubastaYa.Infrastructure.Persistence.Repositories;
using SubastaYa.Infrastructure.Persistence.Seeding;
using SubastaYa.Infrastructure.RealTime;
using SubastaYa.Infrastructure.Security;
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

        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddScoped<ITokenGenerator, JwtTokenGenerator>();

        services.AddScoped<IAuctionRepository, AuctionRepository>();
        services.AddScoped<ICategoryRepository, CategoryRepository>();
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IWalletRepository, WalletRepository>();
        services.AddScoped<ILedgerRepository, LedgerRepository>();
        services.AddScoped<IBidRepository, BidRepository>();
        services.AddScoped<IAuditRepository, AuditRepository>();

        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // Mientras no exista el transporte en tiempo real, la difusión sólo deja traza en el log.
        services.AddSingleton<IAuctionNotifier, LoggingAuctionNotifier>();

        services.AddScoped<DatabaseSeeder>();

        return services;
    }
}
