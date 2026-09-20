using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Api.Controllers;

/// <summary>
/// Consulta sobre la auditoría. Se expone de sólo lectura: los registros son inmutables y los
/// crea únicamente el propio sistema desde los casos de uso que audita.
/// Requiere sesión porque el detalle revela montos e identificadores de terceros.
/// </summary>
[ApiController]
[Route("api/v1/audit-logs")]
[Authorize]
[Produces("application/json")]
public sealed class AuditLogsController : ControllerBase
{
    private const int DefaultCount = 50;

    private readonly IAuditService _audit;

    public AuditLogsController(IAuditService audit)
    {
        _audit = audit;
    }

    /// <summary>Devuelve los últimos eventos auditados, del más reciente al más antiguo.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<AuditRecordDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<AuditRecordDto>>> List(
        CancellationToken cancellationToken,
        [FromQuery] int count = DefaultCount) =>
        Ok(await _audit.GetLatestAsync(count, cancellationToken));
}
