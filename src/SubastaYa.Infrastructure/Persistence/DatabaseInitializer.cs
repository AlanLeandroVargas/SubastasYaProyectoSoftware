using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SubastaYa.Infrastructure.Persistence.Seeding;

namespace SubastaYa.Infrastructure.Persistence;

/// <summary>
/// Deja la base lista para operar: aplica las migraciones pendientes y carga los datos semilla
/// si la base todavía está vacía.
/// </summary>
public static class DatabaseInitializer
{
    public static async Task PrepareDatabaseAsync(
        this IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        using var scope = serviceProvider.CreateScope();

        var context = scope.ServiceProvider.GetRequiredService<SubastaYaDbContext>();
        await context.Database.MigrateAsync(cancellationToken);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(cancellationToken);
    }
}
