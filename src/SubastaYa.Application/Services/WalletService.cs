using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Application.Dtos;
using SubastaYa.Application.Mapping;
using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;
using SubastaYa.Domain.Exceptions;
using SubastaYa.Domain.Rules;

namespace SubastaYa.Application.Services;

/// <summary>
/// Consulta de saldos, acreditación de fondos simulados e historial de movimientos.
/// Toda alteración del saldo deja su asiento en el libro mayor, de manera que el balance pueda
/// reconstruirse sumando el historial y cualquier diferencia delate un error.
/// </summary>
public sealed class WalletService : IWalletService
{
    /// <summary>Tope de movimientos que puede pedir una sola consulta del historial.</summary>
    private const int MaximumLedgerEntries = 200;

    private readonly IWalletRepository _wallets;
    private readonly ILedgerRepository _ledger;
    private readonly IAuditRepository _auditRecords;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public WalletService(
        IWalletRepository wallets,
        ILedgerRepository ledger,
        IAuditRepository auditRecords,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _wallets = wallets;
        _ledger = ledger;
        _auditRecords = auditRecords;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<BalanceDto> GetBalanceAsync(int userId, CancellationToken cancellationToken = default)
    {
        var wallet = await GetWalletAsync(userId, cancellationToken);

        return wallet.ToBalanceDto();
    }

    /// <summary>
    /// Acredita fondos simulados.
    /// El nuevo saldo, su asiento y el registro de auditoría se confirman con un único guardado:
    /// la unidad de trabajo los vuelca en una sola operación atómica, así que no existe la
    /// posibilidad de que el dinero entre sin dejar rastro ni de que quede un asiento sin respaldo.
    /// </summary>
    public async Task<BalanceDto> CreditAsync(
        int userId,
        DepositRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureDepositAmountIsAcceptable(request.Amount);

        var wallet = await GetWalletAsync(userId, cancellationToken);

        var now = _clock.UtcNow;

        wallet.Credit(request.Amount);
        _ledger.Add(LedgerEntry.Deposit(wallet.Id, request.Amount, now));
        _auditRecords.Add(AuditRecordFactory.Create(
            AuditedEntity.Wallet,
            wallet.Id,
            AuditAction.ManualWalletCredit,
            userId,
            new { amount = request.Amount, resultingTotalBalance = wallet.TotalBalance },
            now));

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return wallet.ToBalanceDto();
    }

    public async Task<IReadOnlyList<LedgerEntryDto>> GetLedgerAsync(
        int userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var wallet = await GetWalletAsync(userId, cancellationToken);

        var entries = await _ledger.GetByWalletAsync(
            wallet.Id,
            Math.Clamp(count, 1, MaximumLedgerEntries),
            cancellationToken);

        return entries.Select(entry => entry.ToDto()).ToList();
    }

    private async Task<Wallet> GetWalletAsync(int userId, CancellationToken cancellationToken) =>
        await _wallets.GetByUserAsync(userId, cancellationToken)
        ?? throw new ResourceNotFoundException("la billetera del usuario", userId);

    /// <summary>
    /// El importe positivo ya lo exige la billetera; acá se agrega el tope por operación, que es
    /// una política de la plataforma y no una invariante del saldo.
    /// </summary>
    private static void EnsureDepositAmountIsAcceptable(decimal amount)
    {
        if (amount <= decimal.Zero)
        {
            throw new ValidationException("El monto a acreditar debe ser mayor a cero.");
        }

        if (amount > AuctionRules.MaximumDepositPerOperation)
        {
            throw new ValidationException(
                $"El monto a acreditar no puede superar {AuctionRules.MaximumDepositPerOperation:N2} por operación.");
        }
    }
}
