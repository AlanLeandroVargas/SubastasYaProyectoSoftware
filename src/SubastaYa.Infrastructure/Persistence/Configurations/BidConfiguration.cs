using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Configurations;

internal sealed class BidConfiguration : IEntityTypeConfiguration<Bid>
{
    public void Configure(EntityTypeBuilder<Bid> builder)
    {
        builder.ToTable("Bids", table => table.HasCheckConstraint("CK_Bids_PositiveAmount", "[Amount] > 0"));

        builder.HasKey(bid => bid.Id);

        builder.Property(bid => bid.Amount).HasPrecision(18, 2).IsRequired();
        builder.Property(bid => bid.PlacedAt).IsRequired();

        builder.HasOne(bid => bid.Auction)
            .WithMany(auction => auction.Bids)
            .HasForeignKey(bid => bid.AuctionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(bid => bid.Bidder)
            .WithMany(user => user.PlacedBids)
            .HasForeignKey(bid => bid.BidderId)
            .OnDelete(DeleteBehavior.Restrict);

        // Ordena el historial de la subasta y resuelve la búsqueda de la oferta líder.
        builder.HasIndex(bid => new { bid.AuctionId, bid.Amount });
        builder.HasIndex(bid => new { bid.BidderId, bid.AuctionId });
    }
}
