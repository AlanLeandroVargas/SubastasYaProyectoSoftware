using Microsoft.EntityFrameworkCore;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;

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

    public void Add(Auction auction) => _context.Auctions.Add(auction);

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

    /// <summary>
    /// Carga la subasta <b>con</b> seguimiento de cambios, porque la puja va a modificarla.
    /// No incluye el historial de ofertas: el agregado ya conserva el importe líder y la
    /// cantidad de pujas, así que traerlo entero sería pagar por datos que nadie va a usar.
    /// </summary>
    public async Task<Auction?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default) =>
        await _context.Auctions
            .FirstOrDefaultAsync(auction => auction.Id == id, cancellationToken);

    /// <summary>
    /// Candidatas a cierre. Se leen sin seguimiento porque el proceso vuelve a cargarlas una
    /// por una dentro de su propia transacción: entre esta lectura y ese momento el estado
    /// puede haber cambiado, y es ahí donde se decide de verdad.
    /// </summary>
    public async Task<IReadOnlyList<Auction>> GetExpiredAsync(
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken = default) =>
        await _context.Auctions
            .AsNoTracking()
            .Where(auction => auction.Status == AuctionStatus.Active && auction.EndsAt <= utcNow)
            .OrderBy(auction => auction.EndsAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Con seguimiento: la activación es un cambio de estado que se aplica sobre estas mismas
    /// instancias y se confirma en una única transacción.
    /// </summary>
    public async Task<IReadOnlyList<Auction>> GetScheduledToActivateAsync(
        DateTime utcNow,
        int limit,
        CancellationToken cancellationToken = default) =>
        await _context.Auctions
            .Where(auction => auction.Status == AuctionStatus.Scheduled && auction.StartsAt <= utcNow)
            .OrderBy(auction => auction.StartsAt)
            .Take(limit)
            .ToListAsync(cancellationToken);

    /// <summary>Incluye al postor líder para poder nombrar al ganador de las ya adjudicadas.</summary>
    public async Task<IReadOnlyList<Auction>> GetPublishedByAsync(
        int sellerId,
        CancellationToken cancellationToken = default) =>
        await _context.Auctions
            .AsNoTracking()
            .Include(auction => auction.LeadingBidder)
            .Where(auction => auction.SellerId == sellerId)
            .OrderByDescending(auction => auction.EndsAt)
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Include filtrado: al panel del usuario sólo le interesan las ofertas de este postor, no
    /// el historial completo de cada subasta en la que participó.
    /// </summary>
    public async Task<IReadOnlyList<Auction>> GetParticipatedByAsync(
        int bidderId,
        CancellationToken cancellationToken = default) =>
        await _context.Auctions
            .AsNoTracking()
            .Include(auction => auction.Bids.Where(bid => bid.BidderId == bidderId))
            .Where(auction => auction.Bids.Any(bid => bid.BidderId == bidderId))
            .OrderByDescending(auction => auction.EndsAt)
            .ToListAsync(cancellationToken);

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
