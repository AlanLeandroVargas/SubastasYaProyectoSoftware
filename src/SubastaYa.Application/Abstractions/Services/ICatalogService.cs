using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.Services;

/// <summary>
/// Consultas de sólo lectura del catálogo de subastas.
/// Las vistas que se personalizan reciben el identificador del espectador; nulo significa
/// visitante anónimo, y el catálogo sigue siendo público en ese caso.
/// </summary>
public interface ICatalogService
{
    Task<PagedResult<AuctionSummaryDto>> SearchAsync(AuctionFilter filter, CancellationToken cancellationToken = default);

    Task<AuctionDetailDto> GetDetailAsync(int auctionId, int? viewerId = null, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BidDto>> GetBidHistoryAsync(
        int auctionId,
        int? viewerId,
        int count,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken = default);
}
