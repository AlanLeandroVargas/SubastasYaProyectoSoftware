using Microsoft.Extensions.Logging;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.RealTime;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Application.Dtos;
using SubastaYa.Application.Mapping;
using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;
using SubastaYa.Domain.Exceptions;
using SubastaYa.Domain.Results;

namespace SubastaYa.Application.Services;

/// <summary>
/// Caso de uso central de la plataforma: registra una oferta moviendo las garantías de los
/// postores dentro de un único bloque transaccional y difunde el resultado a la sala en vivo.
///
/// El orden importa. Primero se valida contra el agregado, después se mueven las billeteras y
/// recién entonces se registra la puja: si el saldo no alcanza, nada llegó a modificarse.
/// </summary>
public sealed class BiddingService : IBiddingService
{
    private readonly IAuctionRepository _auctions;
    private readonly IWalletRepository _wallets;
    private readonly ILedgerRepository _ledger;
    private readonly IAuditRepository _auditRecords;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IAuctionNotifier _notifier;
    private readonly ILogger<BiddingService> _logger;

    public BiddingService(
        IAuctionRepository auctions,
        IWalletRepository wallets,
        ILedgerRepository ledger,
        IAuditRepository auditRecords,
        IUnitOfWork unitOfWork,
        IClock clock,
        IAuctionNotifier notifier,
        ILogger<BiddingService> logger)
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

    public async Task<BidResultDto> PlaceBidAsync(
        int auctionId,
        int bidderId,
        PlaceBidRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureAmountIsPositive(request.Amount);

        try
        {
            var confirmation = await _unitOfWork.ExecuteInTransactionAsync(
                token => PlaceBidAtomicallyAsync(auctionId, bidderId, request.Amount, token),
                cancellationToken);

            // La difusión ocurre fuera de la transacción: recién cuando la puja quedó confirmada
            // en la base tiene sentido contársela al resto de la sala. Anunciarla antes podría
            // publicar una oferta que después se revierte.
            await _notifier.NotifyBidPlacedAsync(confirmation.Notification, cancellationToken);

            return confirmation.Result;
        }
        catch (ConcurrencyConflictException exception)
        {
            await AuditRejectedAttemptAsync(
                auctionId, bidderId, request.Amount,
                AuditAction.BidRejectedByConcurrency, exception.Message, cancellationToken);
            throw;
        }
        catch (DomainException exception)
        {
            await AuditRejectedAttemptAsync(
                auctionId, bidderId, request.Amount,
                AuditAction.BidRejectedByValidation, exception.Message, cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Secuencia atómica que exige el negocio: congelar la garantía del nuevo líder, liberar la
    /// del postor desplazado, registrar la puja con su eventual extensión anti-sniping y dejar
    /// constancia de todo en el libro mayor y en la auditoría.
    /// Cualquier falla revierte el bloque completo, de modo que no puede quedar dinero congelado
    /// respaldando una oferta que nunca se registró.
    /// </summary>
    private async Task<ConfirmedBid> PlaceBidAtomicallyAsync(
        int auctionId,
        int bidderId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var auction = await _auctions.GetForUpdateAsync(auctionId, cancellationToken)
                      ?? throw new ResourceNotFoundException("la subasta", auctionId);

        var now = _clock.UtcNow;

        // Se valida antes de tocar las billeteras: una oferta inadmisible no debe llegar a mover
        // saldo ni siquiera dentro de una transacción que después se revierta.
        auction.EnsureBidIsAdmissible(bidderId, amount, now);

        var outbidBidderId = auction.LeadingBidderId;
        var escrowToRelease = auction.CurrentAmount;

        var wallets = await _wallets.GetByUsersAsync(
            IdentifyInvolvedWallets(bidderId, outbidBidderId),
            cancellationToken);

        var bidderWallet = LocateWallet(wallets, bidderId);

        bidderWallet.Hold(amount);
        _ledger.Add(LedgerEntry.Hold(bidderWallet.Id, amount, now, auction.Id));

        if (outbidBidderId is int displacedBidderId)
        {
            var displacedWallet = LocateWallet(wallets, displacedBidderId);

            displacedWallet.ReleaseHold(escrowToRelease);
            _ledger.Add(LedgerEntry.Release(displacedWallet.Id, escrowToRelease, now, auction.Id));
        }

        var result = auction.PlaceBid(bidderId, amount, now);

        AuditBid(auction, bidderId, result, amount, now);

        if (result.WasExtended)
        {
            AuditExtension(auction, bidderId, result.PreviousEndsAt, now);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return BuildConfirmation(auction, bidderWallet, result, outbidBidderId);
    }

    private void AuditBid(
        Auction auction,
        int bidderId,
        BidPlacementResult result,
        decimal amount,
        DateTime now) =>
        _auditRecords.Add(AuditRecordFactory.Create(
            AuditedEntity.Auction,
            auction.Id,
            AuditAction.BidPlaced,
            bidderId,
            new
            {
                amount,
                outbidBidderId = result.OutbidBidderId,
                releasedAmount = result.ReleasedAmount,
                bidCount = auction.BidCount
            },
            now));

    private void AuditExtension(
        Auction auction,
        int bidderId,
        DateTime previousEndsAt,
        DateTime now) =>
        _auditRecords.Add(AuditRecordFactory.Create(
            AuditedEntity.Auction,
            auction.Id,
            AuditAction.AntiSnipingExtension,
            bidderId,
            new { previousEndsAtUtc = previousEndsAt, newEndsAtUtc = auction.EndsAt },
            now));

    /// <summary>
    /// Registra el intento fallido. Corre después de la reversión y en una escritura propia,
    /// porque la auditoría tiene que sobrevivir justamente a la transacción que se revirtió.
    /// </summary>
    private async Task AuditRejectedAttemptAsync(
        int auctionId,
        int bidderId,
        decimal amount,
        AuditAction action,
        string reason,
        CancellationToken cancellationToken)
    {
        try
        {
            _unitOfWork.DiscardPendingChanges();

            _auditRecords.Add(AuditRecordFactory.Create(
                AuditedEntity.Auction,
                auctionId,
                action,
                bidderId,
                new { amount, reason },
                _clock.UtcNow));

            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception exception)
        {
            // Una falla al auditar no debe reemplazar al error de negocio que el usuario tiene
            // que ver: se deja traza en el log y se propaga la excepción original.
            _logger.LogError(
                exception,
                "No se pudo auditar la puja rechazada del usuario {UserId} sobre la subasta {AuctionId}.",
                bidderId,
                auctionId);
        }
    }

    private static ConfirmedBid BuildConfirmation(
        Auction auction,
        Wallet bidderWallet,
        BidPlacementResult result,
        int? outbidBidderId)
    {
        var bid = result.PlacedBid;

        var response = new BidResultDto(
            bid.Id,
            auction.Id,
            bid.Amount,
            auction.MinimumNextBid,
            auction.EndsAt,
            result.WasExtended,
            bidderWallet.ToBalanceDto());

        var notification = new BidPlacedEvent(
            auction.Id,
            auction.CurrentAmount,
            auction.MinimumNextBid,
            auction.BidCount,
            bid.BidderId,
            bidderWallet.User.Pseudonym,
            bid.PlacedAt,
            auction.EndsAt,
            result.WasExtended,
            outbidBidderId);

        return new ConfirmedBid(response, notification);
    }

    /// <summary>
    /// Billeteras que intervienen en la operación. Cuando no hay postor desplazado, o cuando el
    /// desplazado es el propio ofertante, alcanza con una sola.
    /// </summary>
    private static IReadOnlyCollection<int> IdentifyInvolvedWallets(int bidderId, int? outbidBidderId) =>
        outbidBidderId is int displaced && displaced != bidderId
            ? new[] { bidderId, displaced }
            : new[] { bidderId };

    private static Wallet LocateWallet(IReadOnlyList<Wallet> wallets, int userId) =>
        wallets.FirstOrDefault(wallet => wallet.UserId == userId)
        ?? throw new ResourceNotFoundException("la billetera del usuario", userId);

    private static void EnsureAmountIsPositive(decimal amount)
    {
        if (amount <= decimal.Zero)
        {
            throw new ValidationException("El monto ofertado debe ser mayor a cero.");
        }
    }

    /// <summary>Datos que sobreviven a la transacción, usados para responder y para difundir.</summary>
    private sealed record ConfirmedBid(BidResultDto Result, BidPlacedEvent Notification);
}
