namespace SubastaYa.Domain.Exceptions;

/// <summary>Email o contraseña incorrectos. Se traduce a HTTP 401.</summary>
public sealed class InvalidCredentialsException : DomainException
{
    public InvalidCredentialsException() : base("Las credenciales proporcionadas no son válidas.")
    {
    }
}
