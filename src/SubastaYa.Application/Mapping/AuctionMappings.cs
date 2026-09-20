using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Mapping;

/// <summary>
/// Traduce entidades de dominio a los contratos públicos de la API.
/// Se hace a mano y en un único lugar para que el modelo de dominio no quede condicionado por
/// las necesidades de la vista ni por convenciones de una librería de mapeo.
///
/// Las vistas que dependen de quién mira reciben el identificador del espectador como parámetro
/// opcional: nulo significa visitante anónimo, y en ese caso ninguna marca personal se enciende.
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

    public static AuctionDetailDto ToDetailDto(this Auction auction, int? viewerId = null) => new(
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
        viewerId is not null && viewerId == auction.SellerId,
        viewerId is not null && viewerId == auction.LeadingBidderId,
        auction.LeadingBidder?.Pseudonym,
        auction.Bids
            .OrderByDescending(bid => bid.Amount)
            .ThenByDescending(bid => bid.Id)
            .Select(bid => bid.ToDto(viewerId))
            .ToList());

    public static BidDto ToDto(this Bid bid, int? viewerId = null) => new(
        bid.Id,
        bid.Amount,
        bid.Bidder?.Pseudonym ?? "Postor anónimo",
        bid.PlacedAt,
        viewerId is not null && viewerId == bid.BidderId);

    public static CategoryDto ToDto(this Category category) =>
        new(category.Id, category.Name, category.IconUrl);
}
