using Microsoft.EntityFrameworkCore;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Repositories;

/// <summary>
/// Acceso a datos del agregado Auction sobre Entity Framework Core.
/// Las consultas de sólo lectura se ejecutan sin seguimiento de cambios para no pagar el costo
/// del rastreador en las pantallas del catálogo.
/// </summary>
internal sealed class AuctionRepository : IAuctionRepository
{
    private const int MaximumBidsInDetail = 25;

    private readonly SubastaYaDbContext _context;

    public AuctionRepository(SubastaYaDbContext context)
    {
        _context = context;
    }

    public async Task<PagedResult<Auction>> SearchAsync(
        AuctionFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(_context.Auctions.AsNoTracking().Include(auction => auction.Category), filter);
        var total = await query.CountAsync(cancellationToken);

        var items = await ApplySort(query, filter.Sort)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Auction>(items, filter.Page, filter.PageSize, total);
    }

    public async Task<Auction?> GetDetailAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.Auctions
            .AsNoTracking()
            .Include(auction => auction.Category)
            .Include(auction => auction.Seller)
            .Include(auction => auction.LeadingBidder)
            .Include(auction => auction.Bids
                .OrderByDescending(bid => bid.Amount)
                .Take(MaximumBidsInDetail))
            .ThenInclude(bid => bid.Bidder)
            .FirstOrDefaultAsync(auction => auction.Id == id, cancellationToken);

    private static IQueryable<Auction> ApplyFilters(IQueryable<Auction> query, AuctionFilter filter)
    {
        if (filter.Status is not null)
        {
            query = query.Where(auction => auction.Status == filter.Status);
        }

        if (filter.CategoryId is not null)
        {
            query = query.Where(auction => auction.CategoryId == filter.CategoryId);
        }

        if (filter.MinPrice is not null)
        {
            query = query.Where(auction => auction.CurrentAmount >= filter.MinPrice);
        }

        if (filter.MaxPrice is not null)
        {
            query = query.Where(auction => auction.CurrentAmount <= filter.MaxPrice);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            query = query.Where(auction => auction.Title.Contains(filter.Search));
        }

        return query;
    }

    private static IQueryable<Auction> ApplySort(IQueryable<Auction> query, AuctionSort sort) => sort switch
    {
        AuctionSort.HighestBid => query.OrderByDescending(auction => auction.CurrentAmount)
            .ThenBy(auction => auction.Id),
        AuctionSort.Newest => query.OrderByDescending(auction => auction.Id),
        _ => query.OrderBy(auction => auction.EndsAt).ThenBy(auction => auction.Id)
    };
}
