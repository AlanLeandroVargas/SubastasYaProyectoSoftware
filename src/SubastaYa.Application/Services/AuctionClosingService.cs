using Microsoft.Extensions.Logging;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.RealTime;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;
using SubastaYa.Domain.Exceptions;

namespace SubastaYa.Application.Services;

/// <summary>
/// Lógica del proceso en segundo plano: activa las subastas programadas que ya comenzaron y
/// resuelve las vencidas, adjudicándolas con su liquidación final o declarándolas desiertas.
///
/// Cada subasta se procesa en su propia transacción. Así un conflicto puntual no bloquea el lote
/// entero: la subasta afectada se reintenta en la pasada siguiente y el resto avanza igual.
///
/// La lógica vive acá y no en el <c>BackgroundService</c> a propósito: el disparador podría ser
/// igual de bien una tarea programada externa o un endpoint administrativo.
/// </summary>
public sealed class AuctionClosingService : IAuctionClosingService
{
    /// <summary>Cantidad máxima de subastas que resuelve una sola pasada.</summary>
    private const int BatchSize = 50;

    private readonly IAuctionRepository _auctions;
    private readonly IWalletRepository _wallets;
    private readonly ILedgerRepository _ledger;
    private readonly IAuditRepository _auditRecords;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuctionNotifier _notifier;
    private readonly ILogger<AuctionClosingService> _logger;

    public AuctionClosingService(
        IAuctionRepository auctions,
        IWalletRepository wallets,
        ILedgerRepository ledger,
        IAuditRepository auditRecords,
        IUnitOfWork unitOfWork,
        IClock clock,
        IAuctionNotifier notifier,
        ILogger<AuctionClosingService> logger)
    {
        _auctions = auctions;
        _wallets = wallets;
        _ledger = ledger;
        _auditRecords = auditRecords;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<AuctionProcessingSummary> ProcessPendingAuctionsAsync(
        CancellationToken cancellationToken = default)
    {
        var now = _clock.UtcNow;
        var activated = await ActivateScheduledAuctionsAsync(now, cancellationToken);
        var expired = await _auctions.GetExpiredAsync(now, BatchSize, cancellationToken);

        var completed = 0;
        var unsold = 0;
        var conflicts = 0;

        foreach (var candidate in expired)
        {
            try
            {
                var closure = await _unitOfWork.ExecuteInTransactionAsync(
                    token => CloseAuctionAsync(candidate.Id, token),
                    cancellationToken);

                if (closure is null)
                {
                    continue;
                }

                if (closure.WinnerId is null)
                {
                    unsold++;
                }
                else
                {
                    completed++;
                }

                // Igual que con las pujas, se difunde recién con el cierre ya confirmado.
                await _notifier.NotifyAuctionClosedAsync(closure, cancellationToken);
            }
            catch (ConcurrencyConflictException)
            {
                // Una puja de último segundo tocó la subasta mientras se la cerraba. No es un
                // error: se reintenta en la pasada siguiente, ya con la extensión aplicada.
                conflicts++;
                _unitOfWork.DiscardPendingChanges();

                _logger.LogWarning(
                    "Conflicto de concurrencia al cerrar la subasta {AuctionId}. Se reintentará en la próxima pasada.",
                    candidate.Id);
            }
            catch (DomainException exception)
            {
                _unitOfWork.DiscardPendingChanges();
                _logger.LogError(exception, "No se pudo cerrar la subasta {AuctionId}.", candidate.Id);
            }
        }

        return new AuctionProcessingSummary(activated, completed, unsold, conflicts);
    }

    /// <summary>
    /// Pasa a ACTIVA las subastas cuya fecha de inicio ya llegó.
    /// Van todas en una sola transacción porque la activación no mueve dinero: es un cambio de
    /// estado barato y sin efectos cruzados entre subastas.
    /// </summary>
    private async Task<int> ActivateScheduledAuctionsAsync(DateTime now, CancellationToken cancellationToken)
    {
        var scheduled = await _auctions.GetScheduledToActivateAsync(now, BatchSize, cancellationToken);

        if (scheduled.Count == 0)
        {
            return 0;
        }

        return await _unitOfWork.ExecuteInTransactionAsync(async token =>
        {
            var activated = 0;

            foreach (var auction in scheduled)
            {
                if (!auction.ActivateIfDue(now))
                {
                    continue;
                }

                _auditRecords.Add(AuditRecordFactory.Create(
                    AuditedEntity.Auction,
                    auction.Id,
                    AuditAction.AuctionActivated,
                    userId: null,
                    new { startsAtUtc = auction.StartsAt, endsAtUtc = auction.EndsAt },
                    now));

                activated++;
            }

            await _unitOfWork.SaveChangesAsync(token);

            return activated;
        }, cancellationToken);
    }

    /// <summary>
    /// Cierra una subasta vencida.
    /// Devuelve nulo cuando, al releerla dentro de la transacción, otra pasada ya la resolvió o
    /// una extensión anti-sniping la devolvió a la competencia: el candidato se eligió con una
    /// lectura previa, y entre esa lectura y esta transacción el estado pudo cambiar.
    /// </summary>
    private async Task<AuctionClosedEvent?> CloseAuctionAsync(int auctionId, CancellationToken cancellationToken)
    {
        var auction = await _auctions.GetForUpdateAsync(auctionId, cancellationToken);
        var now = _clock.UtcNow;

        if (auction is null || !auction.HasExpired(now))
        {
            return null;
        }

        var notification = auction.HasBids
            ? await AwardAsync(auction, now, cancellationToken)
            : DeclareUnsold(auction, now);

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return notification;
    }

    /// <summary>
    /// Liquidación final: la garantía del ganador sale de su billetera y se acredita al vendedor,
    /// dejando los dos asientos y el registro de auditoría dentro de la misma transacción.
    ///
    /// El comprador usa <c>SettleWithHold</c> y no un débito común: el dinero ya estaba congelado
    /// desde que pasó a liderar, así que liberarlo y descontarlo tiene que ser una sola operación.
    /// </summary>
    private async Task<AuctionClosedEvent> AwardAsync(
        Auction auction,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var winnerId = auction.LeadingBidderId!.Value;
        var finalAmount = auction.CurrentAmount;

        var wallets = await _wallets.GetByUsersAsync(
            new[] { winnerId, auction.SellerId },
            cancellationToken);

        var winnerWallet = LocateWallet(wallets, winnerId);
        var sellerWallet = LocateWallet(wallets, auction.SellerId);

        winnerWallet.SettleWithHold(finalAmount);
        sellerWallet.Credit(finalAmount);

        _ledger.AddRange(new[]
        {
            LedgerEntry.Payment(winnerWallet.Id, finalAmount, now, auction.Id),
            LedgerEntry.Payout(sellerWallet.Id, finalAmount, now, auction.Id)
        });

        auction.AwardToLeadingBidder();

        _auditRecords.Add(AuditRecordFactory.Create(
            AuditedEntity.Auction,
            auction.Id,
            AuditAction.AuctionCompleted,
            userId: null,
            new { winnerId, sellerId = auction.SellerId, finalAmount },
            now));

        return new AuctionClosedEvent(
            auction.Id,
            auction.Status.ToString().ToUpperInvariant(),
            finalAmount,
            winnerId,
            winnerWallet.User.Pseudonym);
    }

    /// <summary>Cierre sin ganador: no hay dinero que mover, sólo el cambio de estado y su traza.</summary>
    private AuctionClosedEvent DeclareUnsold(Auction auction, DateTime now)
    {
        auction.DeclareAsUnsold();

        _auditRecords.Add(AuditRecordFactory.Create(
            AuditedEntity.Auction,
            auction.Id,
            AuditAction.AuctionDeclaredUnsold,
            userId: null,
            new { reason = "La subasta venció sin recibir ninguna oferta." },
            now));

        return new AuctionClosedEvent(
            auction.Id,
            auction.Status.ToString().ToUpperInvariant(),
            FinalAmount: null,
            WinnerId: null,
            WinnerPseudonym: null);
    }

    private static Wallet LocateWallet(IReadOnlyList<Wallet> wallets, int userId) =>
        wallets.FirstOrDefault(wallet => wallet.UserId == userId)
        ?? throw new ResourceNotFoundException("la billetera del usuario", userId);
}
