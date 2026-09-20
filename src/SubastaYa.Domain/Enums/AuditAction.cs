namespace SubastaYa.Domain.Enums;

/// <summary>
/// Eventos críticos del negocio que quedan registrados de forma inmutable.
/// Se persisten como texto, así que sumar acciones nuevas no obliga a renumerar las existentes
/// ni a migrar el historial ya escrito.
/// </summary>
public enum AuditAction
{
    /// <summary>Oferta aceptada: se congeló la garantía y cambió el postor líder.</summary>
    BidPlaced = 1,

    /// <summary>El cierre se corrió por haberse ofertado dentro de la ventana anti-sniping.</summary>
    AntiSnipingExtension = 2,

    /// <summary>Oferta rechazada porque otra petición modificó el mismo registro primero.</summary>
    BidRejectedByConcurrency = 3,

    /// <summary>Oferta rechazada por una regla de negocio: estado, monto, elegibilidad o saldo.</summary>
    BidRejectedByValidation = 4,

    /// <summary>Acreditación manual de fondos simulados en una billetera.</summary>
    ManualWalletCredit = 5
}
