namespace SubastaYa.Api.BackgroundJobs;

/// <summary>Configuración del proceso en segundo plano que resuelve las subastas vencidas.</summary>
public sealed class AuctionWorkerOptions
{
    public const string SectionName = "AuctionWorker";

    /// <summary>
    /// Cada cuánto se revisa el estado de las subastas.
    /// Diez segundos es un punto medio razonable: el cierre se percibe inmediato y la base no
    /// recibe una consulta por segundo sin necesidad.
    /// </summary>
    public int IntervalSeconds { get; init; } = 10;
}
