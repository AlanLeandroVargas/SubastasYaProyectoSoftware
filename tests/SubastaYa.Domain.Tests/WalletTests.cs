using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Exceptions;
using Xunit;

namespace SubastaYa.Domain.Tests;

/// <summary>
/// Verifica las invariantes económicas de la billetera: el saldo disponible nunca puede quedar
/// negativo y una garantía no puede liberarse dos veces.
/// </summary>
public class WalletTests
{
    private static readonly DateTime Now = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    private static Wallet CreateWalletWith(decimal creditedAmount)
    {
        var user = new User("postor@test.com", "Postor", "Postor_X", "hash", Now);

        if (creditedAmount > decimal.Zero)
        {
            user.Wallet.Credit(creditedAmount);
        }

        return user.Wallet;
    }

    [Fact]
    public void NewWallet_StartsAtZero()
    {
        var wallet = CreateWalletWith(decimal.Zero);

        Assert.Equal(decimal.Zero, wallet.TotalBalance);
        Assert.Equal(decimal.Zero, wallet.HeldBalance);
        Assert.Equal(decimal.Zero, wallet.AvailableBalance);
    }

    [Fact]
    public void AvailableBalance_IsTheTotalMinusWhatIsHeld()
    {
        var wallet = CreateWalletWith(150_000m);

        wallet.Hold(45_000m);

        Assert.Equal(150_000m, wallet.TotalBalance);
        Assert.Equal(45_000m, wallet.HeldBalance);
        Assert.Equal(105_000m, wallet.AvailableBalance);
    }

    [Fact]
    public void Hold_MoreThanWhatIsAvailable_IsRejected()
    {
        var wallet = CreateWalletWith(500m);

        var exception = Assert.Throws<InsufficientFundsException>(() => wallet.Hold(55_000m));

        Assert.Equal(500m, exception.AvailableBalance);
        Assert.Equal(55_000m, exception.RequiredAmount);
    }

    [Fact]
    public void Hold_DoesNotChangeTheTotalBalance()
    {
        var wallet = CreateWalletWith(100_000m);

        wallet.Hold(30_000m);

        // La garantía inmoviliza fondos, no los gasta: el total sigue siendo el mismo.
        Assert.Equal(100_000m, wallet.TotalBalance);
    }

    [Fact]
    public void ReleaseHold_ReturnsTheAmountToTheAvailableBalance()
    {
        var wallet = CreateWalletWith(100_000m);
        wallet.Hold(40_000m);

        wallet.ReleaseHold(40_000m);

        Assert.Equal(decimal.Zero, wallet.HeldBalance);
        Assert.Equal(100_000m, wallet.AvailableBalance);
    }

    [Fact]
    public void ReleaseHold_ForMoreThanWhatIsHeld_IsRejected()
    {
        var wallet = CreateWalletWith(100_000m);
        wallet.Hold(10_000m);

        Assert.Throws<StateConflictException>(() => wallet.ReleaseHold(20_000m));
    }

    [Fact]
    public void SettleWithHold_SubtractsTheAmountFromTheTotalAndReleasesTheHold()
    {
        var wallet = CreateWalletWith(230_000m);
        wallet.Hold(30_000m);

        wallet.SettleWithHold(30_000m);

        Assert.Equal(200_000m, wallet.TotalBalance);
        Assert.Equal(decimal.Zero, wallet.HeldBalance);
        Assert.Equal(200_000m, wallet.AvailableBalance);
    }

    [Fact]
    public void SettleWithHold_WithoutTheFundsFrozen_IsRejected()
    {
        var wallet = CreateWalletWith(100_000m);

        // No se puede adjudicar dinero que nunca estuvo respaldado por una retención.
        Assert.Throws<StateConflictException>(() => wallet.SettleWithHold(30_000m));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Credit_WithNonPositiveAmounts_IsRejected(decimal amount)
    {
        var wallet = CreateWalletWith(decimal.Zero);

        Assert.Throws<ValidationException>(() => wallet.Credit(amount));
    }

    [Fact]
    public void Escrow_HandoverOfTheLead_LeavesEachWalletInTheRightState()
    {
        var previousLeader = CreateWalletWith(150_000m);
        var newLeader = CreateWalletWith(200_000m);

        previousLeader.Hold(45_000m);

        // Traspaso: se congela la garantía del nuevo líder y se libera la del desplazado.
        newLeader.Hold(50_000m);
        previousLeader.ReleaseHold(45_000m);

        Assert.Equal(150_000m, previousLeader.AvailableBalance);
        Assert.Equal(decimal.Zero, previousLeader.HeldBalance);
        Assert.Equal(150_000m, newLeader.AvailableBalance);
        Assert.Equal(50_000m, newLeader.HeldBalance);
    }
}
