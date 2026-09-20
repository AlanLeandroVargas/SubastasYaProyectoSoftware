namespace SubastaYa.Domain.Exceptions;

/// <summary>
/// La billetera no tiene saldo disponible suficiente para respaldar la operación.
/// Se traduce a HTTP 422: la petición está bien formada pero es imposible de procesar.
/// </summary>
public sealed class InsufficientFundsException : DomainException
{
    public InsufficientFundsException(decimal availableBalance, decimal requiredAmount)
        : base($"Saldo disponible insuficiente: se requieren {requiredAmount:N2} y sólo hay {availableBalance:N2}.")
    {
        AvailableBalance = availableBalance;
        RequiredAmount = requiredAmount;
    }

    public decimal AvailableBalance { get; }

    public decimal RequiredAmount { get; }
}
