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
    bool IsSeller,
    bool IsLeading,
    string? LeadingBidder,
    IReadOnlyList<BidDto> History);

/// <summary>
/// Oferta tal como se muestra en el historial público, con el postor seudonimizado.
/// <c>IsMine</c> permite que el frontend resalte las ofertas propias sin conocer la
/// correspondencia entre seudónimos e identidades reales.
/// </summary>
public sealed record BidDto(int Id, decimal Amount, string Bidder, DateTime PlacedAtUtc, bool IsMine);

/// <summary>Monto ofertado por un comprador en la sala en vivo.</summary>
public sealed record PlaceBidRequest(decimal Amount);

/// <summary>
/// Resultado de una oferta aceptada, con el nuevo estado económico y temporal.
/// Devuelve el saldo actualizado para que el cliente no tenga que volver a pedirlo y arriesgarse
/// a mostrar un disponible que ya no refleja la garantía recién congelada.
/// </summary>
public sealed record BidResultDto(
    int BidId,
    int AuctionId,
    decimal Amount,
    decimal MinimumNextBid,
    DateTime EndsAtUtc,
    bool WasExtended,
    BalanceDto Balance);

/// <summary>Categoría disponible para clasificar y filtrar subastas.</summary>
public sealed record CategoryDto(int Id, string Name, string IconUrl);
