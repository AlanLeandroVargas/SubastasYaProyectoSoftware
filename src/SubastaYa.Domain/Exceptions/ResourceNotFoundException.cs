namespace SubastaYa.Domain.Exceptions;

/// <summary>El recurso solicitado no existe. Se traduce a HTTP 404.</summary>
public sealed class ResourceNotFoundException : DomainException
{
    public ResourceNotFoundException(string resource, object identifier)
        : base($"No se encontró {resource} con identificador '{identifier}'.")
    {
    }
}
