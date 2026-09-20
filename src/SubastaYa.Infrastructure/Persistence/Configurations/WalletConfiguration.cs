using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Configurations;

internal sealed class WalletConfiguration : IEntityTypeConfiguration<Wallet>
{
    public void Configure(EntityTypeBuilder<Wallet> builder)
    {
        builder.ToTable("Wallets", table => table.HasCheckConstraint(
            "CK_Wallets_CoherentBalances",
            "[TotalBalance] >= 0 AND [HeldBalance] >= 0 AND [TotalBalance] >= [HeldBalance]"));

        builder.HasKey(wallet => wallet.Id);

        builder.Property(wallet => wallet.TotalBalance).HasPrecision(18, 2).IsRequired();
        builder.Property(wallet => wallet.HeldBalance).HasPrecision(18, 2).IsRequired();

        // El saldo disponible es derivado: se calcula en el dominio y no se persiste para evitar
        // redundancia. La restricción CHECK protege la invariante a nivel motor.
        builder.Ignore(wallet => wallet.AvailableBalance);

        // Bloqueo optimista: SQL Server versiona la fila en cada UPDATE.
        builder.Property(wallet => wallet.Version).IsRowVersion();

        builder.HasIndex(wallet => wallet.UserId).IsUnique();
    }
}
