namespace SubastaYa.Application.Dtos;

/// <summary>
/// Desglose de las tres métricas económicas de la billetera.
/// El disponible viaja explícito aunque sea derivado: el frontend no debe replicar la resta ni
/// arriesgarse a mostrar un número distinto al que valida el backend.
/// </summary>
public sealed record BalanceDto(decimal Total, decimal Held, decimal Available);

/// <summary>Acreditación de fondos simulados.</summary>
public sealed record DepositRequest(decimal Amount);

/// <summary>Asiento del libro mayor tal como se muestra en el historial de movimientos.</summary>
public sealed record LedgerEntryDto(
    int Id,
    string Type,
    decimal Amount,
    DateTime OccurredAtUtc,
    int? AuctionId,
    string Description);
