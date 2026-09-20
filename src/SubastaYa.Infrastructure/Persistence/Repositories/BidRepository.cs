using Microsoft.EntityFrameworkCore;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Repositories;

internal sealed class BidRepository : IBidRepository
{
    private readonly SubastaYaDbContext _context;

    public BidRepository(SubastaYaDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<Bid>> GetHistoryAsync(
        int auctionId,
        int count,
        CancellationToken cancellationToken = default) =>
        await _context.Bids
            .AsNoTracking()
            .Include(bid => bid.Bidder)
            .Where(bid => bid.AuctionId == auctionId)
            .OrderByDescending(bid => bid.Amount)
            .ThenByDescending(bid => bid.Id)
            .Take(count)
            .ToListAsync(cancellationToken);
}
