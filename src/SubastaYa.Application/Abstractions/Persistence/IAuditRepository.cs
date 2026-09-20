using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

public interface IAuditRepository
{
    void Add(AuditRecord record);

    /// <summary>Últimos eventos auditados, del más reciente al más antiguo.</summary>
    Task<IReadOnlyList<AuditRecord>> GetLatestAsync(int count, CancellationToken cancellationToken = default);
}
