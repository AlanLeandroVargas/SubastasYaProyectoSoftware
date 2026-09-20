using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;
using SubastaYa.Application.Mapping;
using SubastaYa.Domain.Exceptions;

namespace SubastaYa.Application.Services;

/// <summary>Consultas de sólo lectura sobre el catálogo de subastas.</summary>
public sealed class CatalogService : ICatalogService
{
    /// <summary>Tope de ofertas que devuelve una consulta del historial.</summary>
    private const int MaximumHistorySize = 100;

    private readonly IAuctionRepository _auctions;
    private readonly IBidRepository _bids;
    private readonly ICategoryRepository _categories;

    public CatalogService(
        IAuctionRepository auctions,
        IBidRepository bids,
        ICategoryRepository categories)
    {
        _auctions = auctions;
        _bids = bids;
        _categories = categories;
    }

    public async Task<PagedResult<AuctionSummaryDto>> SearchAsync(
        AuctionFilter filter,
        CancellationToken cancellationToken = default)
    {
        var page = await _auctions.SearchAsync(filter.Normalized(), cancellationToken);

        return page.Map(auction => auction.ToSummaryDto());
    }

    public async Task<AuctionDetailDto> GetDetailAsync(
        int auctionId,
        int? viewerId = null,
        CancellationToken cancellationToken = default)
    {
        var auction = await _auctions.GetDetailAsync(auctionId, cancellationToken)
                      ?? throw new ResourceNotFoundException("la subasta", auctionId);

        return auction.ToDetailDto(viewerId);
    }

    public async Task<IReadOnlyList<BidDto>> GetBidHistoryAsync(
        int auctionId,
        int? viewerId,
        int count,
        CancellationToken cancellationToken = default)
    {
        var bids = await _bids.GetHistoryAsync(
            auctionId,
            Math.Clamp(count, 1, MaximumHistorySize),
            cancellationToken);

        return bids.Select(bid => bid.ToDto(viewerId)).ToList();
    }

    public async Task<IReadOnlyList<CategoryDto>> ListCategoriesAsync(CancellationToken cancellationToken = default)
    {
        var categories = await _categories.ListAsync(cancellationToken);

        return categories.Select(category => category.ToDto()).ToList();
    }
}
