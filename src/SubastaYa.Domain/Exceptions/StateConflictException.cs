namespace SubastaYa.Domain.Exceptions;

/// <summary>
/// La operación es válida en sí misma pero choca con el estado actual del recurso
/// (por ejemplo, pujar en una subasta ya cerrada). Se traduce a HTTP 409.
/// </summary>
public sealed class StateConflictException : DomainException
{
    public StateConflictException(string message) : base(message)
    {
    }
}
