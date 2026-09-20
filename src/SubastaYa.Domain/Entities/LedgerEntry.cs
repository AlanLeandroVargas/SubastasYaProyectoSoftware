using SubastaYa.Domain.Enums;

namespace SubastaYa.Domain.Entities;

/// <summary>
/// Asiento del libro mayor de una billetera. Los asientos son inmutables: toda corrección se
/// expresa con un asiento nuevo, de modo que el saldo siempre pueda reconstruirse a partir del
/// historial.
///
/// Los constructores son privados y el alta pasa por métodos de fábrica con nombre. Así el tipo
/// de movimiento y su descripción no pueden quedar desalineados: quien registra una garantía no
/// puede describirla como un depósito.
///
/// Las descripciones van en español porque se muestran tal cual en la tabla de movimientos.
/// </summary>
public class LedgerEntry
{
    private LedgerEntry()
    {
        // Constructor requerido por EF Core para la materialización.
        Description = string.Empty;
    }

    private LedgerEntry(
        int walletId,
        LedgerEntryType type,
        decimal amount,
        DateTime occurredAt,
        int? auctionId,
        string description)
    {
        WalletId = walletId;
        Type = type;
        Amount = amount;
        OccurredAt = occurredAt;
        AuctionId = auctionId;
        Description = description;
    }

    public int Id { get; private set; }

    public int WalletId { get; private set; }

    public Wallet Wallet { get; private set; } = null!;

    public LedgerEntryType Type { get; private set; }

    /// <summary>Importe del movimiento. Siempre positivo: el signo lo aporta el tipo de asiento.</summary>
    public decimal Amount { get; private set; }

    public DateTime OccurredAt { get; private set; }

    /// <summary>Subasta que originó el movimiento. Nulo en las acreditaciones manuales.</summary>
    public int? AuctionId { get; private set; }

    public Auction? Auction { get; private set; }

    public string Description { get; private set; }

    public static LedgerEntry Deposit(int walletId, decimal amount, DateTime occurredAt) =>
        new(walletId, LedgerEntryType.Deposit, amount, occurredAt, auctionId: null,
            "Acreditación manual de saldo simulado.");

    public static LedgerEntry Hold(int walletId, decimal amount, DateTime occurredAt, int auctionId) =>
        new(walletId, LedgerEntryType.Hold, amount, occurredAt, auctionId,
            $"Garantía congelada por liderar la subasta #{auctionId}.");

    public static LedgerEntry Release(int walletId, decimal amount, DateTime occurredAt, int auctionId) =>
        new(walletId, LedgerEntryType.Release, amount, occurredAt, auctionId,
            $"Garantía liberada al ser superado en la subasta #{auctionId}.");
}
