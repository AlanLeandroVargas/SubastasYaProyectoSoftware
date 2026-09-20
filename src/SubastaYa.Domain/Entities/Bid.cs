namespace SubastaYa.Domain.Entities;

/// <summary>
/// Oferta económica registrada por un comprador sobre una subasta.
/// Es inmutable una vez creada: el historial de pujas forma parte de la trazabilidad del sistema.
/// </summary>
public class Bid
{
    private Bid()
    {
        // Constructor requerido por EF Core para la materialización.
        Auction = null!;
        Bidder = null!;
    }

    internal Bid(Auction auction, int bidderId, decimal amount, DateTime placedAt)
    {
        Auction = auction;
        BidderId = bidderId;
        Amount = amount;
        PlacedAt = placedAt;
    }

    public int Id { get; private set; }

    public int AuctionId { get; private set; }

    public Auction Auction { get; private set; }

    public int BidderId { get; private set; }

    public User Bidder { get; private set; } = null!;

    public decimal Amount { get; private set; }

    public DateTime PlacedAt { get; private set; }
}
