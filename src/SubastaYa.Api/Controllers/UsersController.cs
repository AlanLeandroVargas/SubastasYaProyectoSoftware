using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubastaYa.Application.Abstractions.Security;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Api.Controllers;

/// <summary>
/// Panel "mis actividades": participaciones y publicaciones del usuario autenticado.
/// Igual que en la billetera, se usa "me" y no un identificador: no existe ruta para espiar la
/// actividad de otro.
/// </summary>
[ApiController]
[Route("api/v1/users")]
[Authorize]
[Produces("application/json")]
public sealed class UsersController : ControllerBase
{
    private readonly IUserActivityService _activity;
    private readonly ICurrentUser _currentUser;

    public UsersController(IUserActivityService activity, ICurrentUser currentUser)
    {
        _activity = activity;
        _currentUser = currentUser;
    }

    /// <summary>Subastas en las que participó el usuario, indicando si lidera o ganó.</summary>
    [HttpGet("me/bids")]
    [ProducesResponseType(typeof(IReadOnlyList<ParticipationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<ParticipationDto>>> ListMyParticipations(
        CancellationToken cancellationToken) =>
        Ok(await _activity.GetMyParticipationsAsync(_currentUser.RequireId(), cancellationToken));

    /// <summary>Subastas publicadas por el usuario, con sus métricas de recaudación.</summary>
    [HttpGet("me/auctions")]
    [ProducesResponseType(typeof(IReadOnlyList<PublicationDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<PublicationDto>>> ListMyPublications(
        CancellationToken cancellationToken) =>
        Ok(await _activity.GetMyPublicationsAsync(_currentUser.RequireId(), cancellationToken));
}
