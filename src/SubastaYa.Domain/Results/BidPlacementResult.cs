using SubastaYa.Domain.Entities;

namespace SubastaYa.Domain.Results;

/// <summary>
/// Consecuencias que el dominio deriva de aceptar una puja. El caso de uso las usa para mover
/// la garantía del postor superado, auditar y notificar, sin tener que recalcular nada de lo
/// que ya decidió la entidad <see cref="Auction"/>.
/// </summary>
/// <param name="PlacedBid">Oferta que acaba de incorporarse al historial.</param>
/// <param name="OutbidBidderId">Postor que pierde el liderazgo, o null si era la primera puja.</param>
/// <param name="ReleasedAmount">Garantía que debe devolverse al postor superado.</param>
/// <param name="WasExtended">Indica si se activó la regla anti-sniping.</param>
/// <param name="PreviousEndsAt">Fecha de cierre previa a la extensión, útil para la auditoría.</param>
public sealed record BidPlacementResult(
    Bid PlacedBid,
    int? OutbidBidderId,
    decimal ReleasedAmount,
    bool WasExtended,
    DateTime PreviousEndsAt);
