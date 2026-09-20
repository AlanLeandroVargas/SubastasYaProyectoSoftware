using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.InMemory;

/// <summary>
/// Implementación en memoria del repositorio de subastas.
/// Resuelve los filtros, el ordenamiento y el paginado con LINQ sobre objetos para que el
/// contrato del puerto quede validado antes de que exista la base de datos.
/// </summary>
internal sealed class InMemoryAuctionRepository : IAuctionRepository
{
    private readonly InMemoryCatalogStore _store;

    public InMemoryAuctionRepository(InMemoryCatalogStore store)
    {
        _store = store;
    }

    public Task<PagedResult<Auction>> SearchAsync(
        AuctionFilter filter,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyFilters(_store.Auctions.AsQueryable(), filter);
        var total = query.Count();

        var items = ApplySort(query, filter.Sort)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToList();

        return Task.FromResult(new PagedResult<Auction>(items, filter.Page, filter.PageSize, total));
    }

    public Task<Auction?> GetDetailAsync(int id, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.Auctions.FirstOrDefault(auction => auction.Id == id));

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
            query = query.Where(auction =>
                auction.Title.Contains(filter.Search, StringComparison.OrdinalIgnoreCase));
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
