using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubastaYa.Application.Abstractions.Security;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Api.Controllers;

/// <summary>
/// Recurso principal de la API. Expone el catálogo, el detalle de la sala en vivo, la
/// publicación de subastas y el subrecurso anidado de ofertas.
///
/// Las lecturas siguen siendo públicas: si hay sesión la respuesta se personaliza, y si no la hay
/// se devuelve la misma información sin marcas personales.
/// </summary>
[ApiController]
[Route("api/v1/auctions")]
[Produces("application/json")]
public sealed class AuctionsController : ControllerBase
{
    private const int DefaultHistorySize = 25;

    private readonly ICatalogService _catalog;
    private readonly IAuctionPublishingService _publishing;
    private readonly IBiddingService _bidding;
    private readonly ICurrentUser _currentUser;

    public AuctionsController(
        ICatalogService catalog,
        IAuctionPublishingService publishing,
        IBiddingService bidding,
        ICurrentUser currentUser)
    {
        _catalog = catalog;
        _publishing = publishing;
        _bidding = bidding;
        _currentUser = currentUser;
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
        Ok(await _catalog.GetDetailAsync(id, _currentUser.Id, cancellationToken));

    /// <summary>
    /// Publica una subasta nueva a nombre del usuario autenticado.
    /// Si la fecha de inicio ya pasó, nace ACTIVA; si es futura, queda PROGRAMADA y la activa el
    /// proceso en segundo plano cuando llega el momento.
    /// </summary>
    /// <response code="201">La subasta quedó publicada.</response>
    /// <response code="400">Datos incoherentes: precios, fechas, textos o categoría inexistente.</response>
    [HttpPost]
    [Authorize]
    [ProducesResponseType(typeof(AuctionDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<AuctionDetailDto>> Create(
        [FromBody] CreateAuctionRequest request,
        CancellationToken cancellationToken)
    {
        var published = await _publishing.PublishAsync(request, _currentUser.RequireId(), cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = published.Id }, published);
    }

    /// <summary>Historial de ofertas de la subasta, con los postores seudonimizados.</summary>
    [HttpGet("{id:int}/bids")]
    [ProducesResponseType(typeof(IReadOnlyList<BidDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<BidDto>>> ListBids(
        int id,
        CancellationToken cancellationToken,
        [FromQuery] int count = DefaultHistorySize) =>
        Ok(await _catalog.GetBidHistoryAsync(id, _currentUser.Id, count, cancellationToken));

    /// <summary>
    /// Registra una oferta. Evalúa estado, incremento mínimo, saldo disponible y la regla
    /// anti-sniping dentro de una única transacción atómica.
    /// </summary>
    /// <response code="201">La oferta fue aceptada y los fondos quedaron congelados como garantía.</response>
    /// <response code="400">El monto no alcanza el incremento mínimo exigido.</response>
    /// <response code="403">Un vendedor no puede ofertar en su propia subasta.</response>
    /// <response code="404">La subasta no existe.</response>
    /// <response code="409">La subasta no admite ofertas, o hubo un conflicto de concurrencia.</response>
    /// <response code="422">El saldo disponible no alcanza para respaldar la oferta.</response>
    [HttpPost("{id:int}/bids")]
    [Authorize]
    [ProducesResponseType(typeof(BidResultDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<BidResultDto>> CreateBid(
        int id,
        [FromBody] PlaceBidRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _bidding.PlaceBidAsync(id, _currentUser.RequireId(), request, cancellationToken);

        return CreatedAtAction(nameof(ListBids), new { id }, result);
    }
}
