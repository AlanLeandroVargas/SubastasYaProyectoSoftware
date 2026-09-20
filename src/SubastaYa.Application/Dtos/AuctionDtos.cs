namespace SubastaYa.Application.Dtos;

/// <summary>Vista compacta de una subasta, usada por las tarjetas del catálogo.</summary>
public sealed record AuctionSummaryDto(
    int Id,
    string Title,
    string ImageUrl,
    int CategoryId,
    string Category,
    string Status,
    decimal StartingPrice,
    decimal CurrentAmount,
    decimal MinimumIncrement,
    decimal MinimumNextBid,
    int BidCount,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc);

/// <summary>Vista completa de una subasta, con su historial de ofertas.</summary>
public sealed record AuctionDetailDto(
    int Id,
    string Title,
    string Description,
    string ImageUrl,
    int CategoryId,
    string Category,
    string Status,
    decimal StartingPrice,
    decimal CurrentAmount,
    decimal MinimumIncrement,
    decimal MinimumNextBid,
    int BidCount,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string Seller,
    string? LeadingBidder,
    IReadOnlyList<BidDto> History);

/// <summary>Oferta tal como se muestra en el historial público, con el postor seudonimizado.</summary>
public sealed record BidDto(int Id, decimal Amount, string Bidder, DateTime PlacedAtUtc);

/// <summary>Categoría disponible para clasificar y filtrar subastas.</summary>
public sealed record CategoryDto(int Id, string Name, string IconUrl);
