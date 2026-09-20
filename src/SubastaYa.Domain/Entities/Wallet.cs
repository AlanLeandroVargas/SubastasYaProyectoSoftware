using SubastaYa.Domain.Exceptions;

namespace SubastaYa.Domain.Entities;

/// <summary>
/// Billetera virtual de un usuario. Concentra las únicas operaciones capaces de mover dinero en
/// la plataforma, de modo que ningún servicio pueda alterar los saldos por fuera de las reglas
/// de garantía (escrow).
/// </summary>
public class Wallet
{
    private readonly List<LedgerEntry> _ledgerEntries = new();

    private Wallet()
    {
        // Constructor requerido por EF Core para la materialización.
        User = null!;
        Version = Array.Empty<byte>();
    }

    internal Wallet(User user)
    {
        User = user;
        TotalBalance = decimal.Zero;
        HeldBalance = decimal.Zero;
        Version = Array.Empty<byte>();
    }

    public int Id { get; private set; }

    public int UserId { get; private set; }

    public User User { get; private set; }

    /// <summary>Fondos totales acreditados en la cuenta, incluyendo los que están en garantía.</summary>
    public decimal TotalBalance { get; private set; }

    /// <summary>Monto congelado como garantía en las subastas donde el usuario es postor líder.</summary>
    public decimal HeldBalance { get; private set; }

    /// <summary>
    /// Único dinero utilizable para nuevas pujas o retiros.
    /// Es un dato derivado: no se persiste para evitar redundancia y la base de datos protege la
    /// invariante con una restricción CHECK.
    /// </summary>
    public decimal AvailableBalance => TotalBalance - HeldBalance;

    /// <summary>
    /// Token de concurrencia optimista. La capa de persistencia lo mapea como rowversion para
    /// que dos movimientos simultáneos sobre la misma billetera no puedan pisarse.
    /// </summary>
    public byte[] Version { get; private set; }

    /// <summary>
    /// Libro mayor de la billetera. Es de sólo lectura hacia afuera: los asientos se agregan
    /// desde el caso de uso que mueve el saldo, dentro de la misma transacción.
    /// </summary>
    public IReadOnlyCollection<LedgerEntry> LedgerEntries => _ledgerEntries.AsReadOnly();

    /// <summary>Acredita fondos simulados en la cuenta.</summary>
    public void Credit(decimal amount)
    {
        EnsureAmountIsPositive(amount);
        TotalBalance += amount;
    }

    /// <summary>Congela saldo como garantía de una puja. Falla si el disponible no alcanza.</summary>
    public void Hold(decimal amount)
    {
        EnsureAmountIsPositive(amount);

        if (AvailableBalance < amount)
        {
            throw new InsufficientFundsException(AvailableBalance, amount);
        }

        HeldBalance += amount;
    }

    /// <summary>Devuelve al saldo disponible una garantía previamente congelada.</summary>
    public void ReleaseHold(decimal amount)
    {
        EnsureAmountIsPositive(amount);

        if (HeldBalance < amount)
        {
            throw new StateConflictException(
                $"No es posible liberar {amount:N2}: la billetera sólo tiene {HeldBalance:N2} retenidos.");
        }

        HeldBalance -= amount;
    }

    /// <summary>
    /// Liquidación final del comprador ganador: la garantía deja de estar retenida y sale
    /// definitivamente de la cuenta en una sola operación.
    /// </summary>
    public void SettleWithHold(decimal amount)
    {
        ReleaseHold(amount);
        TotalBalance -= amount;
    }

    private static void EnsureAmountIsPositive(decimal amount)
    {
        if (amount <= decimal.Zero)
        {
            throw new ValidationException("El monto de la operación debe ser mayor a cero.");
        }
    }
}
