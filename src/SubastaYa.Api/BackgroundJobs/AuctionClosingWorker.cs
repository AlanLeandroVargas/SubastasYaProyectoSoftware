using Microsoft.Extensions.Options;
using SubastaYa.Application.Abstractions.Services;

namespace SubastaYa.Api.BackgroundJobs;

/// <summary>
/// Proceso en segundo plano exigido por el negocio. Su única responsabilidad es invocar
/// periódicamente al caso de uso de cierre: toda la lógica de adjudicación y liquidación vive en
/// la capa de aplicación y podría dispararse igual de bien desde una tarea programada externa.
///
/// Esa separación es deliberada. Acá sólo se resuelven las preocupaciones del alojamiento:
/// el intervalo, el alcance de las dependencias y que un error puntual no mate al proceso.
/// </summary>
internal sealed class AuctionClosingWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AuctionClosingWorker> _logger;
    private readonly TimeSpan _interval;

    public AuctionClosingWorker(
        IServiceScopeFactory scopeFactory,
        IOptions<AuctionWorkerOptions> options,
        ILogger<AuctionClosingWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _interval = TimeSpan.FromSeconds(Math.Max(1, options.Value.IntervalSeconds));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Proceso de cierre de subastas iniciado. Intervalo: {Seconds} segundos.",
            _interval.TotalSeconds);

        using var timer = new PeriodicTimer(_interval);

        // Se corre una pasada de entrada y después se espera: al arrancar puede haber subastas
        // vencidas desde la ejecución anterior, y no tiene sentido dejarlas sin resolver.
        do
        {
            await RunPassAsync(stoppingToken);
        }
        while (!stoppingToken.IsCancellationRequested && await WaitForNextPassAsync(timer, stoppingToken));
    }

    private async Task RunPassAsync(CancellationToken stoppingToken)
    {
        try
        {
            // Cada pasada usa su propio alcance: los servicios y el DbContext son scoped, y un
            // contexto compartido entre pasadas acumularía entidades rastreadas indefinidamente.
            using var scope = _scopeFactory.CreateScope();
            var closingService = scope.ServiceProvider.GetRequiredService<IAuctionClosingService>();

            var summary = await closingService.ProcessPendingAuctionsAsync(stoppingToken);

            // Sólo se informa cuando hubo trabajo: con intervalo de 10 segundos, registrar cada
            // pasada vacía ahogaría el log.
            if (summary.HasWork)
            {
                _logger.LogInformation(
                    "Pasada completada. Activadas: {Activated}, adjudicadas: {Completed}, " +
                    "desiertas: {Unsold}, conflictos: {Conflicts}.",
                    summary.Activated,
                    summary.Completed,
                    summary.Unsold,
                    summary.Conflicts);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado ordenado de la aplicación: no es un error.
        }
        catch (Exception exception)
        {
            // Un error puntual no debe matar al proceso: se reintenta en la pasada siguiente.
            _logger.LogError(exception, "Falló una pasada del proceso de cierre de subastas.");
        }
    }

    private static async Task<bool> WaitForNextPassAsync(PeriodicTimer timer, CancellationToken stoppingToken)
    {
        try
        {
            return await timer.WaitForNextTickAsync(stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return false;
        }
    }
}
