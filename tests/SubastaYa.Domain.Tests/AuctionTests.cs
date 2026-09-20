using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;
using SubastaYa.Domain.Exceptions;
using Xunit;

namespace SubastaYa.Domain.Tests;

/// <summary>
/// Verifica las invariantes del agregado Auction. Son pruebas puras: el dominio no depende de la
/// base de datos ni del reloj real, así que cada regla se comprueba en milisegundos.
/// </summary>
public class AuctionTests
{
    private static readonly DateTime Now = new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    private const int SellerId = 1;
    private const int BidderId = 2;
    private const int OtherBidderId = 3;

    private static Auction CreateActiveAuction(
        decimal startingPrice = 10_000m,
        decimal minimumIncrement = 1_000m,
        TimeSpan? timeUntilClosing = null) =>
        Auction.Publish(
            SellerId,
            categoryId: 1,
            "Notebook de prueba",
            "Descripción de prueba suficientemente descriptiva.",
            "https://ejemplo.test/imagen.jpg",
            startingPrice,
            minimumIncrement,
            startsAt: Now.AddMinutes(-10),
            endsAt: Now.Add(timeUntilClosing ?? TimeSpan.FromMinutes(30)),
            Now);

    [Fact]
    public void Publish_WithFutureStartDate_LeavesTheAuctionScheduled()
    {
        var auction = Auction.Publish(
            SellerId, 1, "Título", "Descripción", "https://ejemplo.test/i.jpg",
            10_000m, 1_000m, Now.AddHours(24), Now.AddHours(26), Now);

        Assert.Equal(AuctionStatus.Scheduled, auction.Status);
    }

    [Fact]
    public void Publish_WithEndDateBeforeStartDate_IsRejected()
    {
        var exception = Assert.Throws<ValidationException>(() => Auction.Publish(
            SellerId, 1, "Título", "Descripción", "https://ejemplo.test/i.jpg",
            10_000m, 1_000m, Now.AddHours(2), Now.AddHours(1), Now));

        Assert.Contains("posterior", exception.Message);
    }

    [Theory]
    [InlineData(0, 1_000)]
    [InlineData(-5_000, 1_000)]
    [InlineData(10_000, 0)]
    [InlineData(10_000, 20_000)]
    public void Publish_WithIncoherentPricing_IsRejected(decimal startingPrice, decimal minimumIncrement)
    {
        Assert.Throws<ValidationException>(() => Auction.Publish(
            SellerId, 1, "Título", "Descripción", "https://ejemplo.test/i.jpg",
            startingPrice, minimumIncrement, Now, Now.AddHours(1), Now));
    }

    [Fact]
    public void MinimumNextBid_WithoutOffers_IsTheStartingPrice()
    {
        var auction = CreateActiveAuction(startingPrice: 10_000m, minimumIncrement: 1_000m);

        Assert.Equal(10_000m, auction.MinimumNextBid);
    }

    [Fact]
    public void MinimumNextBid_WithOffers_AddsTheMinimumIncrementToTheLeadingOne()
    {
        var auction = CreateActiveAuction(startingPrice: 10_000m, minimumIncrement: 1_000m);
        auction.PlaceBid(BidderId, 10_000m, Now);

        Assert.Equal(11_000m, auction.MinimumNextBid);
    }

    [Fact]
    public void PlaceBid_BelowTheMinimumIncrement_IsRejected()
    {
        var auction = CreateActiveAuction(startingPrice: 10_000m, minimumIncrement: 1_000m);
        auction.PlaceBid(BidderId, 10_000m, Now);

        Assert.Throws<ValidationException>(() => auction.PlaceBid(OtherBidderId, 10_500m, Now));
    }

    [Fact]
    public void PlaceBid_ReportsTheOutbidBidderAndTheAmountToRelease()
    {
        var auction = CreateActiveAuction(startingPrice: 10_000m, minimumIncrement: 1_000m);
        auction.PlaceBid(BidderId, 10_000m, Now);

        var result = auction.PlaceBid(OtherBidderId, 11_000m, Now);

        Assert.Equal(BidderId, result.OutbidBidderId);
        Assert.Equal(10_000m, result.ReleasedAmount);
        Assert.Equal(OtherBidderId, auction.LeadingBidderId);
        Assert.Equal(2, auction.BidCount);
    }

    [Fact]
    public void PlaceBid_BySellerOnTheirOwnAuction_IsRejected()
    {
        var auction = CreateActiveAuction();

        Assert.Throws<AuthorizationException>(() => auction.PlaceBid(SellerId, 50_000m, Now));
    }

    [Fact]
    public void PlaceBid_ByTheBidderAlreadyLeading_IsRejected()
    {
        var auction = CreateActiveAuction(startingPrice: 10_000m, minimumIncrement: 1_000m);
        auction.PlaceBid(BidderId, 10_000m, Now);

        Assert.Throws<StateConflictException>(() => auction.PlaceBid(BidderId, 20_000m, Now));
    }

    [Fact]
    public void PlaceBid_OnAScheduledAuction_IsRejected()
    {
        var auction = Auction.Publish(
            SellerId, 1, "Título", "Descripción", "https://ejemplo.test/i.jpg",
            10_000m, 1_000m, Now.AddHours(24), Now.AddHours(26), Now);

        Assert.Throws<StateConflictException>(() => auction.PlaceBid(BidderId, 10_000m, Now));
    }

    [Fact]
    public void PlaceBid_AfterClosing_IsRejected()
    {
        var auction = CreateActiveAuction(timeUntilClosing: TimeSpan.FromMinutes(5));

        Assert.Throws<StateConflictException>(() => auction.PlaceBid(BidderId, 10_000m, Now.AddMinutes(6)));
    }

    // --- Regla anti-sniping -------------------------------------------------

    [Fact]
    public void PlaceBid_WithinTheLastMinute_ExtendsTheClosingByTwoMinutes()
    {
        var auction = CreateActiveAuction(timeUntilClosing: TimeSpan.FromSeconds(45));
        var originalClosing = auction.EndsAt;

        var result = auction.PlaceBid(BidderId, 10_000m, Now);

        Assert.True(result.WasExtended);
        Assert.Equal(originalClosing.AddMinutes(2), auction.EndsAt);
        Assert.Equal(originalClosing, result.PreviousEndsAt);
    }

    [Fact]
    public void PlaceBid_ExactlyAtTheSixtySecondBoundary_ExtendsTheClosing()
    {
        var auction = CreateActiveAuction(timeUntilClosing: TimeSpan.FromSeconds(60));

        var result = auction.PlaceBid(BidderId, 10_000m, Now);

        Assert.True(result.WasExtended);
    }

    [Fact]
    public void PlaceBid_OutsideTheCriticalWindow_DoesNotExtendTheClosing()
    {
        var auction = CreateActiveAuction(timeUntilClosing: TimeSpan.FromSeconds(61));
        var originalClosing = auction.EndsAt;

        var result = auction.PlaceBid(BidderId, 10_000m, Now);

        Assert.False(result.WasExtended);
        Assert.Equal(originalClosing, auction.EndsAt);
    }

    // --- Cierre -------------------------------------------------------------

    [Fact]
    public void HasExpired_IsTrueOnlyWhenTheActiveAuctionPassedItsClosing()
    {
        var auction = CreateActiveAuction(timeUntilClosing: TimeSpan.FromMinutes(5));

        Assert.False(auction.HasExpired(Now.AddMinutes(4)));
        Assert.True(auction.HasExpired(Now.AddMinutes(5)));
    }

    [Fact]
    public void AwardToLeadingBidder_WithBids_MarksTheAuctionAsCompleted()
    {
        var auction = CreateActiveAuction();
        auction.PlaceBid(BidderId, 10_000m, Now);

        auction.AwardToLeadingBidder();

        Assert.Equal(AuctionStatus.Completed, auction.Status);
    }

    [Fact]
    public void AwardToLeadingBidder_WithoutBids_IsRejected()
    {
        var auction = CreateActiveAuction();

        Assert.Throws<StateConflictException>(() => auction.AwardToLeadingBidder());
    }

    [Fact]
    public void DeclareAsUnsold_WithoutBids_MarksTheAuctionAsUnsold()
    {
        var auction = CreateActiveAuction();

        auction.DeclareAsUnsold();

        Assert.Equal(AuctionStatus.Unsold, auction.Status);
    }

    [Fact]
    public void DeclareAsUnsold_WithBids_IsRejected()
    {
        var auction = CreateActiveAuction();
        auction.PlaceBid(BidderId, 10_000m, Now);

        Assert.Throws<StateConflictException>(() => auction.DeclareAsUnsold());
    }

    [Fact]
    public void ActivateIfDue_WhenTheStartDateArrives_ActivatesTheAuction()
    {
        var auction = Auction.Publish(
            SellerId, 1, "Título", "Descripción", "https://ejemplo.test/i.jpg",
            10_000m, 1_000m, Now.AddHours(2), Now.AddHours(4), Now);

        Assert.False(auction.ActivateIfDue(Now.AddHours(1)));
        Assert.True(auction.ActivateIfDue(Now.AddHours(2)));
        Assert.Equal(AuctionStatus.Active, auction.Status);
    }
}
