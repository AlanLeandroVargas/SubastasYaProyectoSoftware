namespace SubastaYa.Application.Dtos;

/// <summary>
/// Fila de la solapa "mis compras / pujas".
/// <c>MyHighestBid</c> y <c>CurrentAmount</c> viajan por separado a propósito: la diferencia
/// entre ambos es justamente lo que le dice al usuario por cuánto lo superaron.
/// </summary>
public sealed record ParticipationDto(
    int AuctionId,
    string Title,
    string ImageUrl,
    string Status,
    decimal MyHighestBid,
    decimal CurrentAmount,
    int BidCount,
    bool IsLeading,
    bool HasWon,
    DateTime EndsAtUtc);

/// <summary>Fila de la solapa "mis publicaciones", con las métricas de recaudación.</summary>
public sealed record PublicationDto(
    int AuctionId,
    string Title,
    string ImageUrl,
    string Status,
    decimal StartingPrice,
    decimal CurrentAmount,
    int BidCount,
    decimal Revenue,
    string? Winner,
    DateTime EndsAtUtc);
