using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.Services;

/// <summary>Consultas de sólo lectura del catálogo de subastas.</summary>
public interface ICatalogService
{
    Task<PagedResult<AuctionSummaryDto>> SearchAsync(AuctionFilter filter, CancellationToken cancellationToken = default);

    Task<AuctionDetailDto> GetDetailAsync(int auctionId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken = default);
}
