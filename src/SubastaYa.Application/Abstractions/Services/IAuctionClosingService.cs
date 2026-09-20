namespace SubastaYa.Application.Abstractions.Services;

/// <summary>
/// Caso de uso que ejecuta el proceso en segundo plano: activa las subastas programadas que ya
/// comenzaron y adjudica o declara desiertas las que vencieron.
/// </summary>
public interface IAuctionClosingService
{
    Task<AuctionProcessingSummary> ProcessPendingAuctionsAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Métricas de una pasada del proceso, útiles para el log operativo.
/// <c>Conflicts</c> no cuenta errores: son subastas que otra operación tocó mientras se las
/// cerraba y que se reintentan en la pasada siguiente.
/// </summary>
public sealed record AuctionProcessingSummary(int Activated, int Completed, int Unsold, int Conflicts)
{
    public static AuctionProcessingSummary Empty => new(0, 0, 0, 0);

    public bool HasWork => Activated + Completed + Unsold + Conflicts > 0;
}
