using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;
using Xunit;

namespace SubastaYa.Domain.Tests;

/// <summary>
/// Verifica que cada fábrica de asientos produzca el tipo correcto y que el libro mayor alcance
/// para reconstruir el saldo de una billetera sin consultar sus columnas.
/// </summary>
public class LedgerEntryTests
{
    private static readonly DateTime Now = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    private const int WalletId = 7;
    private const int AuctionId = 42;

    [Fact]
    public void Deposit_HasNoAssociatedAuction()
    {
        var entry = LedgerEntry.Deposit(WalletId, 150_000m, Now);

        Assert.Equal(LedgerEntryType.Deposit, entry.Type);
        Assert.Equal(150_000m, entry.Amount);
        Assert.Null(entry.AuctionId);
    }

    [Fact]
    public void Hold_ReferencesTheAuctionThatFrozeTheFunds()
    {
        var entry = LedgerEntry.Hold(WalletId, 45_000m, Now, AuctionId);

        Assert.Equal(LedgerEntryType.Hold, entry.Type);
        Assert.Equal(AuctionId, entry.AuctionId);
        Assert.Contains($"#{AuctionId}", entry.Description);
    }

    [Fact]
    public void Release_ReferencesTheAuctionThatReturnedTheFunds()
    {
        var entry = LedgerEntry.Release(WalletId, 45_000m, Now, AuctionId);

        Assert.Equal(LedgerEntryType.Release, entry.Type);
        Assert.Equal(AuctionId, entry.AuctionId);
        Assert.Contains($"#{AuctionId}", entry.Description);
    }

    [Fact]
    public void EveryEntry_StoresItsAmountAsAPositiveNumber()
    {
        // El signo lo aporta el tipo de asiento, no el importe: así la restricción CHECK de la
        // base puede exigir que todo movimiento sea mayor a cero.
        LedgerEntry[] entries =
        [
            LedgerEntry.Deposit(WalletId, 1_000m, Now),
            LedgerEntry.Hold(WalletId, 1_000m, Now, AuctionId),
            LedgerEntry.Release(WalletId, 1_000m, Now, AuctionId)
        ];

        Assert.All(entries, entry => Assert.True(entry.Amount > decimal.Zero));
    }

    [Fact]
    public void TheLedger_ReconstructsTheBalanceOfTheWallet()
    {
        var user = new User("postor@test.com", "Postor", "Postor_X", "hash", Now);
        var wallet = user.Wallet;

        wallet.Credit(150_000m);
        wallet.Hold(45_000m);
        wallet.ReleaseHold(45_000m);
        wallet.Hold(50_000m);

        LedgerEntry[] ledger =
        [
            LedgerEntry.Deposit(WalletId, 150_000m, Now),
            LedgerEntry.Hold(WalletId, 45_000m, Now, AuctionId),
            LedgerEntry.Release(WalletId, 45_000m, Now, AuctionId),
            LedgerEntry.Hold(WalletId, 50_000m, Now, AuctionId)
        ];

        var total = ledger
            .Where(entry => entry.Type == LedgerEntryType.Deposit)
            .Sum(entry => entry.Amount);

        var held = ledger.Sum(entry => entry.Type switch
        {
            LedgerEntryType.Hold => entry.Amount,
            LedgerEntryType.Release => -entry.Amount,
            _ => decimal.Zero
        });

        Assert.Equal(wallet.TotalBalance, total);
        Assert.Equal(wallet.HeldBalance, held);
        Assert.Equal(wallet.AvailableBalance, total - held);
    }
}
