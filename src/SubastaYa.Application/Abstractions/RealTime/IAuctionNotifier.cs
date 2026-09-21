using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.RealTime;

/// <summary>
/// Canal de difusión hacia la sala de subasta en vivo. La capa de aplicación publica eventos de
/// negocio sin saber si por debajo hay WebSockets, SignalR o cualquier otro transporte.
///
/// El puerto se declaró antes de que existiera el transporte, y eso permitió que el caso de uso
/// de pujas quedara completo con una implementación que sólo dejaba traza en el log. Cambiarla
/// por SignalR consistió en registrar otra implementación: nada de esta capa se modificó.
/// </summary>
public interface IAuctionNotifier
{
    Task NotifyBidPlacedAsync(BidPlacedEvent notification, CancellationToken cancellationToken = default);

    Task NotifyAuctionClosedAsync(AuctionClosedEvent notification, CancellationToken cancellationToken = default);
}
