using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

public interface IWalletRepository
{
    Task<Wallet?> GetByUserAsync(int userId, CancellationToken cancellationToken = default);
}
