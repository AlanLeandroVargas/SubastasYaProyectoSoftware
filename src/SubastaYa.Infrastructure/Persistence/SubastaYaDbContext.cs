using Microsoft.EntityFrameworkCore;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence;

/// <summary>
/// Contexto de persistencia Code-First. El esquema relacional completo se deriva de estas
/// entidades y se materializa en la base de datos mediante migraciones.
/// </summary>
public sealed class SubastaYaDbContext : DbContext
{
    public SubastaYaDbContext(DbContextOptions<SubastaYaDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();

    public DbSet<Wallet> Wallets => Set<Wallet>();

    public DbSet<Category> Categories => Set<Category>();

    public DbSet<Auction> Auctions => Set<Auction>();

    public DbSet<Bid> Bids => Set<Bid>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.ApplyConfigurationsFromAssembly(typeof(SubastaYaDbContext).Assembly);
        UtcDateTimeConvention.ApplyToAllDateTimes(builder);

        base.OnModelCreating(builder);
    }
}
