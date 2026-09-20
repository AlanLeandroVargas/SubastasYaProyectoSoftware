namespace SubastaYa.Domain.Exceptions;

/// <summary>
/// Dos operaciones simultáneas intentaron modificar la misma fila y el bloqueo optimista rechazó
/// a la más lenta. Se traduce a HTTP 409 para que el cliente pueda reintentar.
///
/// Se distingue de <see cref="StateConflictException"/> aunque compartan código HTTP: aquélla
/// señala una operación imposible, ésta una carrera en la que reintentar tiene sentido.
/// </summary>
public sealed class ConcurrencyConflictException : DomainException
{
    public ConcurrencyConflictException(string message) : base(message)
    {
    }
}
