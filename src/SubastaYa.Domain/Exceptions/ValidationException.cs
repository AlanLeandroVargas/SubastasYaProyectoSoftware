namespace SubastaYa.Domain.Exceptions;

/// <summary>Datos de entrada incoherentes o fuera de rango. Se traduce a HTTP 400.</summary>
public sealed class ValidationException : DomainException
{
    public ValidationException(string message) : base(message)
    {
    }
}
