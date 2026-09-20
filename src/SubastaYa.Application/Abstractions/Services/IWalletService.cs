using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.Services;

public interface IWalletService
{
    Task<BalanceDto> GetBalanceAsync(int userId, CancellationToken cancellationToken = default);

    Task<BalanceDto> CreditAsync(int userId, DepositRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LedgerEntryDto>> GetLedgerAsync(
        int userId,
        int count,
        CancellationToken cancellationToken = default);
}
