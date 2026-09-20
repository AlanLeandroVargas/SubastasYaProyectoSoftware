using Microsoft.EntityFrameworkCore;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Repositories;

internal sealed class LedgerRepository : ILedgerRepository
{
    private readonly SubastaYaDbContext _context;

    public LedgerRepository(SubastaYaDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Deja el asiento pendiente en el contexto. La confirmación queda en manos de la unidad de
    /// trabajo, que es la que sabe con qué otras escrituras tiene que viajar.
    /// </summary>
    public void Add(LedgerEntry entry) => _context.LedgerEntries.Add(entry);

    public void AddRange(IEnumerable<LedgerEntry> entries) => _context.LedgerEntries.AddRange(entries);

    public async Task<IReadOnlyList<LedgerEntry>> GetByWalletAsync(
        int walletId,
        int count,
        CancellationToken cancellationToken = default) =>
        await _context.LedgerEntries
            .AsNoTracking()
            .Where(entry => entry.WalletId == walletId)
            // El identificador desempata los asientos que comparten instante, como el congelamiento
            // de una garantía y la liberación de la anterior dentro de la misma puja.
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
}
