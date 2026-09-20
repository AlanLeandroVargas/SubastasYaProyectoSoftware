using Microsoft.EntityFrameworkCore;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Repositories;

internal sealed class AuditRepository : IAuditRepository
{
    private readonly SubastaYaDbContext _context;

    public AuditRepository(SubastaYaDbContext context)
    {
        _context = context;
    }

    public void Add(AuditRecord record) => _context.AuditRecords.Add(record);

    /// <summary>
    /// Ordena por identificador y no por fecha: los registros se numeran de forma estrictamente
    /// creciente, así que varios eventos del mismo instante conservan su orden real de ocurrencia.
    /// </summary>
    public async Task<IReadOnlyList<AuditRecord>> GetLatestAsync(
        int count,
        CancellationToken cancellationToken = default) =>
        await _context.AuditRecords
            .AsNoTracking()
            .OrderByDescending(record => record.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
}
