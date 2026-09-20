using System.Text.Json;
using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;

namespace SubastaYa.Application.Services;

/// <summary>
/// Construye las entradas de auditoría serializando su detalle a JSON.
/// Centralizar la construcción evita que cada caso de uso invente su propia forma de payload y
/// que el mismo dato aparezca con nombres distintos según quién lo haya escrito.
/// </summary>
public static class AuditRecordFactory
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static AuditRecord Create(
        AuditedEntity entity,
        int entityId,
        AuditAction action,
        int? userId,
        object details,
        DateTime occurredAtUtc) =>
        new(entity, entityId, action, userId, JsonSerializer.Serialize(details, SerializerOptions), occurredAtUtc);
}
