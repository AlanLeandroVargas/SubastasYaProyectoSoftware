using Microsoft.AspNetCore.Mvc;

namespace SubastaYa.Api.Controllers;

/// <summary>
/// Sonda de disponibilidad de la API. Es el primer endpoint del proyecto y existe para que
/// tanto el proceso de despliegue como el desarrollador puedan confirmar que el servicio está
/// levantado antes de que haya funcionalidad de negocio disponible.
/// </summary>
[ApiController]
[Route("api/v1/health")]
[Produces("application/json")]
public sealed class HealthController : ControllerBase
{
    /// <summary>Devuelve el estado actual del servicio.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(HealthResponse), StatusCodes.Status200OK)]
    public ActionResult<HealthResponse> Get() =>
        Ok(new HealthResponse("ok", DateTime.UtcNow));

    /// <summary>Respuesta mínima de la sonda de disponibilidad.</summary>
    public sealed record HealthResponse(string Status, DateTime CheckedAtUtc);
}
