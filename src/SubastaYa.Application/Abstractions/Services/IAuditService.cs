using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.Services;

public interface IAuditService
{
    Task<IReadOnlyList<AuditRecordDto>> GetLatestAsync(int count, CancellationToken cancellationToken = default);
}
