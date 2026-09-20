using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Mapping;

/// <summary>
/// Traduce entidades de dominio a los contratos públicos de la API.
/// Se hace a mano y en un único lugar para que el modelo de dominio no quede condicionado por
/// las necesidades de la vista ni por convenciones de una librería de mapeo.
/// </summary>
public static class AuctionMappings
{
    public static AuctionSummaryDto ToSummaryDto(this Auction auction) => new(
        auction.Id,
        auction.Title,
        auction.ImageUrl,
        auction.CategoryId,
        auction.Category?.Name ?? string.Empty,
        auction.Status.ToString().ToUpperInvariant(),
        auction.StartingPrice,
        auction.CurrentAmount,
        auction.MinimumIncrement,
        auction.MinimumNextBid,
        auction.BidCount,
        auction.StartsAt,
        auction.EndsAt);

    public static AuctionDetailDto ToDetailDto(this Auction auction) => new(
        auction.Id,
        auction.Title,
        auction.Description,
        auction.ImageUrl,
        auction.CategoryId,
        auction.Category?.Name ?? string.Empty,
        auction.Status.ToString().ToUpperInvariant(),
        auction.StartingPrice,
        auction.CurrentAmount,
        auction.MinimumIncrement,
        auction.MinimumNextBid,
        auction.BidCount,
        auction.StartsAt,
        auction.EndsAt,
        auction.Seller?.Pseudonym ?? string.Empty,
        auction.LeadingBidder?.Pseudonym,
        auction.Bids
            .OrderByDescending(bid => bid.Amount)
            .ThenByDescending(bid => bid.Id)
            .Select(bid => bid.ToDto())
            .ToList());

    public static BidDto ToDto(this Bid bid) => new(
        bid.Id,
        bid.Amount,
        bid.Bidder?.Pseudonym ?? "Postor anónimo",
        bid.PlacedAt);

    public static CategoryDto ToDto(this Category category) =>
        new(category.Id, category.Name, category.IconUrl);
}
