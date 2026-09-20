using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

public interface ILedgerRepository
{
    void Add(LedgerEntry entry);

    /// <summary>Asienta de una vez los dos movimientos de una liquidación: débito y crédito.</summary>
    void AddRange(IEnumerable<LedgerEntry> entries);

    /// <summary>Devuelve los movimientos más recientes de una billetera, del más nuevo al más viejo.</summary>
    Task<IReadOnlyList<LedgerEntry>> GetByWalletAsync(
        int walletId,
        int count,
        CancellationToken cancellationToken = default);
}
