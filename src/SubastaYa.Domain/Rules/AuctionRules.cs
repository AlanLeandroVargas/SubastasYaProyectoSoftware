namespace SubastaYa.Domain.Rules;

/// <summary>
/// Parámetros de negocio que gobiernan el motor de subastas.
/// Se centralizan acá para que la regla anti-sniping sea auditable en un único lugar y no quede
/// replicada en servicios, controladores o el frontend.
/// </summary>
public static class AuctionRules
{
    /// <summary>Ventana previa al cierre dentro de la cual una puja dispara la extensión automática.</summary>
    public static readonly TimeSpan AntiSnipingWindow = TimeSpan.FromSeconds(60);

    /// <summary>Tiempo que se suma a la fecha de finalización cuando se activa la regla anti-sniping.</summary>
    public static readonly TimeSpan AntiSnipingExtension = TimeSpan.FromMinutes(2);

    /// <summary>Duración mínima admitida entre el inicio y el fin de una subasta.</summary>
    public static readonly TimeSpan MinimumDuration = TimeSpan.FromMinutes(1);
}
