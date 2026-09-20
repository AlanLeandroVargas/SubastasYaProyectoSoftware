using Microsoft.AspNetCore.SignalR;
using SubastaYa.Application.Abstractions.RealTime;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Api.Hubs;

/// <summary>
/// Publica los eventos de negocio en el grupo SignalR de la subasta correspondiente.
///
/// Reemplaza al notificador provisional que sólo dejaba traza en el log. El cambio se resolvió
/// registrando otra implementación del mismo puerto: ni el servicio de pujas, ni el de cierre,
/// ni el dominio cambiaron una línea.
/// </summary>
internal sealed class SignalRAuctionNotifier : IAuctionNotifier
{
    private readonly IHubContext<AuctionHub> _hub;
    private readonly ILogger<SignalRAuctionNotifier> _logger;

    public SignalRAuctionNotifier(IHubContext<AuctionHub> hub, ILogger<SignalRAuctionNotifier> logger)
    {
        _hub = hub;
        _logger = logger;
    }

    public Task NotifyBidPlacedAsync(BidPlacedEvent notification, CancellationToken cancellationToken = default) =>
        BroadcastAsync(notification.AuctionId, AuctionHub.BidPlacedEvent, notification, cancellationToken);

    public Task NotifyAuctionClosedAsync(AuctionClosedEvent notification, CancellationToken cancellationToken = default) =>
        BroadcastAsync(notification.AuctionId, AuctionHub.AuctionClosedEvent, notification, cancellationToken);

    private async Task BroadcastAsync(
        int auctionId,
        string eventName,
        object payload,
        CancellationToken cancellationToken)
    {
        try
        {
            await _hub.Clients
                .Group(AuctionHub.GroupName(auctionId))
                .SendAsync(eventName, payload, cancellationToken);
        }
        catch (Exception exception)
        {
            // La operación de negocio ya está confirmada en la base: un fallo al difundir no debe
            // llegar al usuario ni revertir nada. El cliente se resincroniza en su próxima
            // consulta, así que la difusión es una mejora de experiencia, no la fuente de verdad.
            _logger.LogWarning(
                exception,
                "No se pudo difundir el evento {EventName} de la subasta {AuctionId}.",
                eventName,
                auctionId);
        }
    }
}
