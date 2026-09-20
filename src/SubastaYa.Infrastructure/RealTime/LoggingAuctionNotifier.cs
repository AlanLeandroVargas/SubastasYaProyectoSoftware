using Microsoft.Extensions.Logging;
using SubastaYa.Application.Abstractions.RealTime;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Infrastructure.RealTime;

/// <summary>
/// Implementación provisional del canal de difusión: deja el evento en el log en lugar de
/// empujarlo a los clientes conectados.
///
/// Existe para que el caso de uso de pujas quede terminado sin arrastrar todavía la
/// infraestructura de tiempo real. Cuando llegue SignalR, el reemplazo se limita a registrar otra
/// implementación de <see cref="IAuctionNotifier"/>: ni el servicio de pujas ni el dominio se
/// enteran del cambio.
///
/// Mientras tanto cumple una función real: hace observable en el log qué se habría difundido,
/// que es exactamente lo que después habrá que ver llegar a la sala.
/// </summary>
internal sealed class LoggingAuctionNotifier : IAuctionNotifier
{
    private readonly ILogger<LoggingAuctionNotifier> _logger;

    public LoggingAuctionNotifier(ILogger<LoggingAuctionNotifier> logger)
    {
        _logger = logger;
    }

    public Task NotifyBidPlacedAsync(
        BidPlacedEvent notification,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Difusión pendiente de transporte. Subasta {AuctionId}: {Pseudonym} ofertó {Amount:N2}, " +
            "próximo mínimo {MinimumNextBid:N2}, cierre {EndsAt:O}, extendida: {WasExtended}.",
            notification.AuctionId,
            notification.BidderPseudonym,
            notification.CurrentAmount,
            notification.MinimumNextBid,
            notification.EndsAtUtc,
            notification.WasExtended);

        return Task.CompletedTask;
    }

    public Task NotifyAuctionClosedAsync(
        AuctionClosedEvent notification,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "Difusión pendiente de transporte. Subasta {AuctionId} cerrada como {Status}. " +
            "Ganador: {Winner}, importe final: {FinalAmount}.",
            notification.AuctionId,
            notification.Status,
            notification.WinnerPseudonym ?? "sin ganador",
            notification.FinalAmount);

        return Task.CompletedTask;
    }
}
