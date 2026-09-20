using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Dtos;
using SubastaYa.Application.Mapping;

namespace SubastaYa.Application.Services;

/// <summary>
/// Expone la auditoría en modo sólo lectura.
/// No hay operación de alta ni de baja: los registros los escribe el propio sistema dentro de
/// los casos de uso que audita, y una vez escritos no se tocan.
/// </summary>
public sealed class AuditService : IAuditService
{
    /// <summary>Tope de registros que puede pedir una sola consulta.</summary>
    private const int MaximumRecords = 200;

    private readonly IAuditRepository _auditRecords;

    public AuditService(IAuditRepository auditRecords)
    {
        _auditRecords = auditRecords;
    }

    public async Task<IReadOnlyList<AuditRecordDto>> GetLatestAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        var records = await _auditRecords.GetLatestAsync(Math.Clamp(count, 1, MaximumRecords), cancellationToken);

        return records.Select(record => record.ToDto()).ToList();
    }
}
