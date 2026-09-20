using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Mapping;

/// <summary>Traduce la billetera y su libro mayor a los contratos públicos de la API.</summary>
public static class WalletMappings
{
    public static BalanceDto ToBalanceDto(this Wallet wallet) =>
        new(wallet.TotalBalance, wallet.HeldBalance, wallet.AvailableBalance);

    public static LedgerEntryDto ToDto(this LedgerEntry entry) => new(
        entry.Id,
        entry.Type.ToString().ToUpperInvariant(),
        entry.Amount,
        entry.OccurredAt,
        entry.AuctionId,
        entry.Description);
}
