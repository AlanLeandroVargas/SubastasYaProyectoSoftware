namespace SubastaYa.Domain.Entities;

/// <summary>
/// Participante de la plataforma. Un mismo usuario puede actuar como vendedor (publicando
/// subastas) y como comprador (pujando en las de otros).
/// </summary>
public class User
{
    private readonly List<Auction> _publishedAuctions = new();
    private readonly List<Bid> _placedBids = new();

    private User()
    {
        // Constructor requerido por EF Core para la materialización.
        Email = string.Empty;
        Name = string.Empty;
        Pseudonym = string.Empty;
        PasswordHash = string.Empty;
        Wallet = null!;
    }

    public User(string email, string name, string pseudonym, string passwordHash, DateTime registeredAt)
    {
        Email = email.Trim().ToLowerInvariant();
        Name = name.Trim();
        Pseudonym = pseudonym.Trim();
        PasswordHash = passwordHash;
        RegisteredAt = registeredAt;

        // Todo usuario nace con su billetera: no existe participación sin respaldo económico.
        Wallet = new Wallet(this);
    }

    public int Id { get; private set; }

    public string Email { get; private set; }

    public string Name { get; private set; }

    /// <summary>
    /// Alias público usado en el historial de ofertas para no exponer la identidad real de los
    /// postores dentro de la sala en vivo.
    /// </summary>
    public string Pseudonym { get; private set; }

    public string PasswordHash { get; private set; }

    public DateTime RegisteredAt { get; private set; }

    public Wallet Wallet { get; private set; }

    public IReadOnlyCollection<Auction> PublishedAuctions => _publishedAuctions.AsReadOnly();

    public IReadOnlyCollection<Bid> PlacedBids => _placedBids.AsReadOnly();
}
