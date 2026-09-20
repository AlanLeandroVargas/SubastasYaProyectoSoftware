using SubastaYa.Domain.Enums;

namespace SubastaYa.Domain.Entities;

/// <summary>
/// Entrada inmutable de trazabilidad. Complementa al libro mayor y al historial de ofertas
/// registrando por qué y a instancias de quién se disparó cada evento crítico del sistema.
///
/// A diferencia del libro mayor, que sólo conoce movimientos de dinero, la auditoría también
/// guarda los intentos <b>rechazados</b>: son los que explican por qué una puja no prosperó.
/// </summary>
public class AuditRecord
{
    private AuditRecord()
    {
        // Constructor requerido por EF Core para la materialización.
        DetailsJson = string.Empty;
    }

    public AuditRecord(
        AuditedEntity entity,
        int entityId,
        AuditAction action,
        int? userId,
        string detailsJson,
        DateTime occurredAt)
    {
        Entity = entity;
        EntityId = entityId;
        Action = action;
        UserId = userId;
        DetailsJson = detailsJson;
        OccurredAt = occurredAt;
    }

    /// <summary>
    /// Identificador de 64 bits: la auditoría crece con cada intento, incluidos los rechazados,
    /// así que es la tabla con más probabilidades de agotar un entero de 32 bits.
    /// </summary>
    public long Id { get; private set; }

    public AuditedEntity Entity { get; private set; }

    /// <summary>Identificador del registro afectado dentro de la entidad auditada.</summary>
    public int EntityId { get; private set; }

    public AuditAction Action { get; private set; }

    /// <summary>Usuario que originó la acción. Nulo cuando la ejecuta el propio sistema.</summary>
    public int? UserId { get; private set; }

    public User? User { get; private set; }

    /// <summary>
    /// Detalle del evento serializado como JSON.
    /// Se guarda sin esquema fijo porque cada acción necesita datos distintos, y forzar columnas
    /// dejaría la tabla llena de campos nulos.
    /// </summary>
    public string DetailsJson { get; private set; }

    public DateTime OccurredAt { get; private set; }
}
