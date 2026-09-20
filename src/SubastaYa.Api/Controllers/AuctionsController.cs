using Microsoft.AspNetCore.Mvc;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Api.Controllers;

/// <summary>
/// Recurso principal de la API. Por ahora expone sólo las operaciones de lectura del catálogo;
/// la publicación y el registro de pujas llegan con sus propias funcionalidades.
/// </summary>
[ApiController]
[Route("api/v1/auctions")]
[Produces("application/json")]
public sealed class AuctionsController : ControllerBase
{
    private readonly ICatalogService _catalog;

    public AuctionsController(ICatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Lista el catálogo con filtros de estado, categoría, rango de precios y orden.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<AuctionSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<PagedResult<AuctionSummaryDto>>> List(
        [FromQuery] AuctionFilter filter,
        CancellationToken cancellationToken) =>
        Ok(await _catalog.SearchAsync(filter, cancellationToken));

    /// <summary>Devuelve el detalle completo de una subasta con su historial de ofertas.</summary>
    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(AuctionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuctionDetailDto>> GetById(int id, CancellationToken cancellationToken) =>
        Ok(await _catalog.GetDetailAsync(id, cancellationToken));
}
