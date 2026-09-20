namespace SubastaYa.Domain.Entities;

/// <summary>Clasificación temática que agrupa las subastas del catálogo.</summary>
public class Category
{
    private readonly List<Auction> _auctions = new();

    private Category()
    {
        // Constructor requerido por EF Core para la materialización.
        Name = string.Empty;
        IconUrl = string.Empty;
    }

    public Category(string name, string iconUrl)
    {
        Name = name;
        IconUrl = iconUrl;
    }

    public int Id { get; private set; }

    public string Name { get; private set; }

    /// <summary>Ícono representativo que el catálogo muestra junto al nombre.</summary>
    public string IconUrl { get; private set; }

    public IReadOnlyCollection<Auction> Auctions => _auctions.AsReadOnly();
}
