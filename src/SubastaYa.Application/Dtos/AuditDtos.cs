namespace SubastaYa.Application.Dtos;

/// <summary>
/// Registro de auditoría expuesto de sólo lectura, para inspección y para defender la práctica.
/// <c>Details</c> viaja como el JSON crudo que se guardó: su forma depende de la acción.
/// </summary>
public sealed record AuditRecordDto(
    long Id,
    string Entity,
    int EntityId,
    string Action,
    int? UserId,
    string Details,
    DateTime OccurredAtUtc);
