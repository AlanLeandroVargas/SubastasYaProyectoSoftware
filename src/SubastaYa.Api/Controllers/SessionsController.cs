using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubastaYa.Application.Abstractions.Security;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Api.Controllers;

/// <summary>
/// Gestión de sesiones. Se modela como recurso ("sessions") para respetar la convención de URLs
/// basadas en sustantivos: abrir sesión es crear una sesión, no invocar un verbo.
/// </summary>
[ApiController]
[Route("api/v1/sessions")]
[Produces("application/json")]
public sealed class SessionsController : ControllerBase
{
    private readonly IAuthenticationService _authentication;
    private readonly ICurrentUser _currentUser;

    public SessionsController(IAuthenticationService authentication, ICurrentUser currentUser)
    {
        _authentication = authentication;
        _currentUser = currentUser;
    }

    /// <summary>Abre una sesión y devuelve el token de acceso.</summary>
    [HttpPost]
    [AllowAnonymous]
    [ProducesResponseType(typeof(SessionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<SessionDto>> Create(
        [FromBody] SignInRequest request,
        CancellationToken cancellationToken) =>
        Ok(await _authentication.SignInAsync(request, cancellationToken));

    /// <summary>Devuelve el perfil asociado a la sesión en curso.</summary>
    [HttpGet("current")]
    [Authorize]
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<UserDto>> GetCurrent(CancellationToken cancellationToken) =>
        Ok(await _authentication.GetProfileAsync(_currentUser.RequireId(), cancellationToken));
}
