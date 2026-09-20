using Microsoft.AspNetCore.Mvc;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Api.Controllers;

/// <summary>Catálogo de categorías disponibles para clasificar y filtrar subastas.</summary>
[ApiController]
[Route("api/v1/categories")]
[Produces("application/json")]
public sealed class CategoriesController : ControllerBase
{
    private readonly ICatalogService _catalog;

    public CategoriesController(ICatalogService catalog)
    {
        _catalog = catalog;
    }

    /// <summary>Lista todas las categorías ordenadas alfabéticamente.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<CategoryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<CategoryDto>>> List(CancellationToken cancellationToken) =>
        Ok(await _catalog.ListCategoriesAsync(cancellationToken));
}
