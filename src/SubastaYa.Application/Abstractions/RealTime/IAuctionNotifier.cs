using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.RealTime;

/// <summary>
/// Canal de difusión hacia la sala de subasta en vivo. La capa de aplicación publica eventos de
/// negocio sin saber si por debajo hay WebSockets, SignalR o cualquier otro transporte.
///
/// Declarar el puerto ahora permite que el caso de uso quede completo aunque el transporte real
/// todavía no exista: hoy lo satisface una implementación que sólo deja traza en el log.
/// </summary>
public interface IAuctionNotifier
{
    Task NotifyBidPlacedAsync(BidPlacedEvent notification, CancellationToken cancellationToken = default);
}
