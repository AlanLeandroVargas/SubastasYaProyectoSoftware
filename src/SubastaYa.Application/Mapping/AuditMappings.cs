using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Mapping;

public static class AuditMappings
{
    public static AuditRecordDto ToDto(this AuditRecord record) => new(
        record.Id,
        record.Entity.ToString().ToUpperInvariant(),
        record.EntityId,
        record.Action.ToString().ToUpperInvariant(),
        record.UserId,
        record.DetailsJson,
        record.OccurredAt);
}
