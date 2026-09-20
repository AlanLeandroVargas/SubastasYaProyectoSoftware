namespace SubastaYa.Domain.Enums;

/// <summary>
/// Naturaleza contable de un movimiento en el libro mayor de una billetera.
/// Se persiste como texto para que el historial siga siendo legible en la base de datos y para
/// que agregar un tipo nuevo no obligue a renumerar los existentes.
/// </summary>
public enum LedgerEntryType
{
    /// <summary>Acreditación de fondos simulados.</summary>
    Deposit = 1,

    /// <summary>Fondos congelados al pasar a ser el postor líder de una subasta.</summary>
    Hold = 2,

    /// <summary>Fondos congelados que vuelven a estar disponibles tras ser superado.</summary>
    Release = 3
}
