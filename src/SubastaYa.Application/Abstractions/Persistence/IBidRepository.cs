using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

public interface IBidRepository
{
    /// <summary>Últimas ofertas de una subasta, de la más alta a la más baja.</summary>
    Task<IReadOnlyList<Bid>> GetHistoryAsync(int auctionId, int count, CancellationToken cancellationToken = default);
}
