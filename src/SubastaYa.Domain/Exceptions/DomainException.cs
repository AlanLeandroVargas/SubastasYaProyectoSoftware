namespace SubastaYa.Domain.Exceptions;

/// <summary>
/// Raíz de la jerarquía de errores previsibles del negocio.
/// La capa de presentación traduce cada subtipo a un código HTTP concreto, de modo que el
/// dominio nunca necesita conocer detalles del protocolo.
/// Los mensajes se escriben en español porque se le muestran tal cual al usuario final.
/// </summary>
public abstract class DomainException : Exception
{
    protected DomainException(string message) : base(message)
    {
    }
}
