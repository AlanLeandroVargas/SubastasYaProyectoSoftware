using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Configurations;

internal sealed class LedgerEntryConfiguration : IEntityTypeConfiguration<LedgerEntry>
{
    public void Configure(EntityTypeBuilder<LedgerEntry> builder)
    {
        builder.ToTable("LedgerEntries", table => table.HasCheckConstraint(
            "CK_LedgerEntries_PositiveAmount",
            "[Amount] > 0"));

        builder.HasKey(entry => entry.Id);

        builder.Property(entry => entry.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(entry => entry.OccurredAt).IsRequired();

        // El tipo se guarda como texto: el historial sigue siendo legible consultando la tabla y
        // agregar un tipo nuevo no obliga a mantener la correspondencia con un número.
        builder.Property(entry => entry.Type)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(entry => entry.Description)
            .HasMaxLength(300)
            .IsRequired();

        builder.HasOne(entry => entry.Wallet)
            .WithMany(wallet => wallet.LedgerEntries)
            .HasForeignKey(entry => entry.WalletId)
            .OnDelete(DeleteBehavior.Cascade);

        // La subasta es opcional: las acreditaciones manuales no provienen de ninguna.
        // Se restringe el borrado para que eliminar una subasta nunca borre la huella contable.
        builder.HasOne(entry => entry.Auction)
            .WithMany()
            .HasForeignKey(entry => entry.AuctionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Resuelve la consulta del historial: filtra por billetera y ordena por fecha.
        builder.HasIndex(entry => new { entry.WalletId, entry.OccurredAt });
    }
}
