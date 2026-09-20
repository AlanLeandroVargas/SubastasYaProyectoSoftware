using SubastaYa.Domain.Exceptions;

namespace SubastaYa.Application.Abstractions.Security;

/// <summary>
/// Identidad del usuario que originó la petición en curso. Permite que los casos de uso conozcan
/// al solicitante sin depender de HttpContext ni de ningún detalle de ASP.NET.
/// </summary>
public interface ICurrentUser
{
    int? Id { get; }

    bool IsAuthenticated { get; }

    /// <summary>Devuelve el identificador autenticado o falla si la petición es anónima.</summary>
    int RequireId() => Id
        ?? throw new AuthorizationException("Se requiere una sesión activa para realizar esta operación.");
}
