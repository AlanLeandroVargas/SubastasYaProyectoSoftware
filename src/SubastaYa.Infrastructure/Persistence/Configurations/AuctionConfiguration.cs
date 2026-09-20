using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Configurations;

internal sealed class AuctionConfiguration : IEntityTypeConfiguration<Auction>
{
    public void Configure(EntityTypeBuilder<Auction> builder)
    {
        builder.ToTable("Auctions", table =>
        {
            table.HasCheckConstraint("CK_Auctions_PositiveAmounts", "[StartingPrice] > 0 AND [MinimumIncrement] > 0");
            table.HasCheckConstraint("CK_Auctions_Schedule", "[EndsAt] > [StartsAt]");
            table.HasCheckConstraint("CK_Auctions_BidCount", "[BidCount] >= 0");
        });

        builder.HasKey(auction => auction.Id);

        builder.Property(auction => auction.Title).IsRequired().HasMaxLength(150);
        builder.Property(auction => auction.Description).IsRequired().HasMaxLength(2000);
        builder.Property(auction => auction.ImageUrl).IsRequired().HasMaxLength(500);

        builder.Property(auction => auction.StartingPrice).HasPrecision(18, 2).IsRequired();
        builder.Property(auction => auction.MinimumIncrement).HasPrecision(18, 2).IsRequired();
        builder.Property(auction => auction.CurrentAmount).HasPrecision(18, 2).IsRequired();

        // El estado se persiste como texto para que la base siga siendo legible sin necesidad de
        // conocer el orden de los valores del enum.
        builder.Property(auction => auction.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(auction => auction.Version).IsRowVersion();

        builder.HasOne(auction => auction.Seller)
            .WithMany(user => user.PublishedAuctions)
            .HasForeignKey(auction => auction.SellerId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(auction => auction.Category)
            .WithMany(category => category.Auctions)
            .HasForeignKey(auction => auction.CategoryId)
            .OnDelete(DeleteBehavior.Restrict);

        // El postor líder es una referencia sin colección inversa: se resuelve desde el historial.
        builder.HasOne(auction => auction.LeadingBidder)
            .WithMany()
            .HasForeignKey(auction => auction.LeadingBidderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Índice que sostiene el filtro del catálogo por estado y cierre.
        builder.HasIndex(auction => new { auction.Status, auction.EndsAt });
        builder.HasIndex(auction => auction.CategoryId);
        builder.HasIndex(auction => auction.SellerId);

        builder.Metadata
            .FindNavigation(nameof(Auction.Bids))!
            .SetPropertyAccessMode(PropertyAccessMode.Field);
    }
}
