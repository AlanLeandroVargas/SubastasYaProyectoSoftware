namespace SubastaYa.Application.Dtos;

/// <summary>
/// Actualización que se difunde a la sala en vivo cada vez que se acepta una oferta.
/// Lleva todo lo que necesita un cliente conectado para repintar la sala sin volver a consultar
/// la API: importe líder, próximo mínimo, cantidad de ofertas y el nuevo cierre.
/// </summary>
public sealed record BidPlacedEvent(
    int AuctionId,
    decimal CurrentAmount,
    decimal MinimumNextBid,
    int BidCount,
    int BidderId,
    string BidderPseudonym,
    DateTime PlacedAtUtc,
    DateTime EndsAtUtc,
    bool WasExtended,
    int? OutbidBidderId);
