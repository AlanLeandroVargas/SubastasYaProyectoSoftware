namespace SubastaYa.Domain.Enums;

/// <summary>Ciclo de vida de una subasta dentro de la plataforma.</summary>
public enum AuctionStatus
{
    /// <summary>Publicada pero con fecha de inicio a futuro: todavía no acepta pujas.</summary>
    Scheduled = 1,

    /// <summary>En curso: acepta pujas hasta la fecha de finalización.</summary>
    Active = 2,

    /// <summary>Cerrada con un postor ganador y la venta ya liquidada.</summary>
    Completed = 3,

    /// <summary>Cerrada sin haber recibido ninguna puja.</summary>
    Unsold = 4
}
