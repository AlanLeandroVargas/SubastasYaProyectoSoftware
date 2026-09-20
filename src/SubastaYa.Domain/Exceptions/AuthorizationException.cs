namespace SubastaYa.Domain.Exceptions;

/// <summary>El usuario autenticado no tiene permiso sobre el recurso. Se traduce a HTTP 403.</summary>
public sealed class AuthorizationException : DomainException
{
    public AuthorizationException(string message) : base(message)
    {
    }
}
