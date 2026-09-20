using System.Reflection;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.InMemory;

/// <summary>
/// Conjunto de datos en memoria que sustituye temporalmente a la base de datos.
///
/// Permite que el catálogo sea una funcionalidad entregable antes de incorporar Entity Framework
/// Core: la API ya responde con datos reales y el contrato queda cerrado, de modo que al llegar
/// la persistencia definitiva sólo se reemplaza la implementación del puerto
/// <c>IAuctionRepository</c> sin tocar ni el dominio ni los controladores.
///
/// Se registra como singleton para que el conjunto se construya una sola vez y las consultas
/// devuelvan siempre los mismos identificadores.
/// </summary>
internal sealed class InMemoryCatalogStore
{
    private int _nextBidId = 1;

    public InMemoryCatalogStore(IClock clock)
    {
        var now = clock.UtcNow;

        var technology = CreateCategory(1, "Tecnología", "bi-cpu");
        var collectibles = CreateCategory(2, "Coleccionables", "bi-gem");
        var clothing = CreateCategory(3, "Indumentaria", "bi-bag-heart");
        var vehicles = CreateCategory(4, "Vehículos", "bi-car-front");

        Categories = new[] { technology, collectibles, clothing, vehicles };

        var seller = CreateUser(1, "vendedor@test.com", "Valeria Vendedora", "VendedorPro", now);
        var firstBidder = CreateUser(2, "comprador1@test.com", "Carlos Comprador", "Postor_A1", now);
        var secondBidder = CreateUser(3, "comprador2@test.com", "Camila Compradora", "Postor_B2", now);

        Auctions = new[]
        {
            BuildStandardActiveAuction(1, seller, firstBidder, secondBidder, technology, now),
            BuildClosingSoonAuction(2, seller, collectibles, now),
            BuildScheduledAuction(3, seller, clothing, now),
            BuildCompletedAuction(4, seller, secondBidder, vehicles, now),
            BuildUnsoldAuction(5, seller, collectibles, now)
        };
    }

    public IReadOnlyList<Category> Categories { get; }

    public IReadOnlyList<Auction> Auctions { get; }

    /// <summary>Subasta en curso con dos ofertas previas y postor líder en $45.000.</summary>
    private Auction BuildStandardActiveAuction(
        int id,
        User seller,
        User firstBidder,
        User secondBidder,
        Category category,
        DateTime now)
    {
        var auction = Auction.Publish(
            seller.Id,
            category.Id,
            "Notebook gamer Legion 16 pulgadas RTX 4070",
            "Notebook de alta gama con 32 GB de RAM, SSD de 1 TB y placa RTX 4070. Usada tres meses, con caja y factura.",
            "https://picsum.photos/seed/notebook/800/600",
            startingPrice: 30_000m,
            minimumIncrement: 5_000m,
            startsAt: now.AddMinutes(-30),
            endsAt: now.AddMinutes(25),
            now);

        LinkNavigations(auction, id, seller, category);
        PlaceBid(auction, secondBidder, 40_000m, now.AddMinutes(-12));
        PlaceBid(auction, firstBidder, 45_000m, now.AddMinutes(-4));

        return auction;
    }

    /// <summary>Subasta en zona crítica, útil para probar el contador regresivo del frontend.</summary>
    private Auction BuildClosingSoonAuction(int id, User seller, Category category, DateTime now)
    {
        var auction = Auction.Publish(
            seller.Id,
            category.Id,
            "Figura coleccionable edición limitada 1/200",
            "Pieza numerada de una tirada de 200 unidades, sellada y con certificado de autenticidad.",
            "https://picsum.photos/seed/figura/800/600",
            startingPrice: 10_000m,
            minimumIncrement: 1_000m,
            startsAt: now.AddMinutes(-20),
            endsAt: now.AddMinutes(3),
            now);

        LinkNavigations(auction, id, seller, category);

        return auction;
    }

    /// <summary>Subasta programada para comenzar en 24 horas: todavía no acepta ofertas.</summary>
    private Auction BuildScheduledAuction(int id, User seller, Category category, DateTime now)
    {
        var auction = Auction.Publish(
            seller.Id,
            category.Id,
            "Campera de cuero vintage talle L",
            "Campera de cuero genuino restaurada, forro interior nuevo y cierres originales.",
            "https://picsum.photos/seed/campera/800/600",
            startingPrice: 18_000m,
            minimumIncrement: 2_000m,
            startsAt: now.AddHours(24),
            endsAt: now.AddHours(26),
            now);

        LinkNavigations(auction, id, seller, category);

        return auction;
    }

    /// <summary>Subasta ya adjudicada, para que el filtro por estado tenga un caso finalizado.</summary>
    private Auction BuildCompletedAuction(int id, User seller, User winner, Category category, DateTime now)
    {
        var auction = Auction.Publish(
            seller.Id,
            category.Id,
            "Moto 150cc modelo 2021 con 12.000 km",
            "Moto en excelente estado, service oficial al día, cubiertas nuevas y papeles en regla.",
            "https://picsum.photos/seed/moto/800/600",
            startingPrice: 25_000m,
            minimumIncrement: 5_000m,
            startsAt: now.AddHours(-3),
            endsAt: now.AddMinutes(10),
            now);

        LinkNavigations(auction, id, seller, category);
        PlaceBid(auction, winner, 30_000m, now.AddHours(-1));
        auction.AwardToLeadingBidder();

        // El cierre se retrocede recién ahora porque el dominio prohíbe publicar una subasta vencida.
        AssignPrivateProperty(auction, nameof(Auction.EndsAt), now.AddMinutes(-2));

        return auction;
    }

    /// <summary>Subasta vencida sin ofertas, para que el filtro por estado tenga un caso desierto.</summary>
    private Auction BuildUnsoldAuction(int id, User seller, Category category, DateTime now)
    {
        var auction = Auction.Publish(
            seller.Id,
            category.Id,
            "Álbum de figuritas Mundial 1986 incompleto",
            "Álbum original de la época con 70% de las figuritas pegadas. Ideal para completar colección.",
            "https://picsum.photos/seed/album/800/600",
            startingPrice: 22_000m,
            minimumIncrement: 2_000m,
            startsAt: now.AddHours(-2),
            endsAt: now.AddMinutes(10),
            now);

        LinkNavigations(auction, id, seller, category);
        auction.DeclareAsUnsold();
        AssignPrivateProperty(auction, nameof(Auction.EndsAt), now.AddMinutes(-5));

        return auction;
    }

    private static Category CreateCategory(int id, string name, string iconUrl)
    {
        var category = new Category(name, iconUrl);
        AssignPrivateProperty(category, nameof(Category.Id), id);

        return category;
    }

    private static User CreateUser(int id, string email, string name, string pseudonym, DateTime now)
    {
        // La contraseña es irrelevante mientras no exista autenticación: el catálogo es público.
        var user = new User(email, name, pseudonym, passwordHash: string.Empty, registeredAt: now);
        AssignPrivateProperty(user, nameof(User.Id), id);

        return user;
    }

    /// <summary>Registra una puja y completa los datos que en la base resolvería el ORM.</summary>
    private void PlaceBid(Auction auction, User bidder, decimal amount, DateTime placedAt)
    {
        var result = auction.PlaceBid(bidder.Id, amount, placedAt);

        AssignPrivateProperty(result.PlacedBid, nameof(Bid.Id), _nextBidId++);
        AssignPrivateProperty(result.PlacedBid, nameof(Bid.Bidder), bidder);
        AssignPrivateProperty(auction, nameof(Auction.LeadingBidder), bidder);
    }

    private static void LinkNavigations(Auction auction, int id, User seller, Category category)
    {
        AssignPrivateProperty(auction, nameof(Auction.Id), id);
        AssignPrivateProperty(auction, nameof(Auction.Seller), seller);
        AssignPrivateProperty(auction, nameof(Auction.Category), category);
    }

    /// <summary>
    /// Asigna una propiedad de setter privado.
    ///
    /// Las entidades exponen setters privados a propósito: los identificadores y las propiedades
    /// de navegación los completa el ORM al materializar el grafo. Como todavía no hay ORM, el
    /// conjunto de prueba hace ese mismo trabajo por reflexión. Este método desaparece junto con
    /// la implementación en memoria cuando entre Entity Framework Core.
    /// </summary>
    private static void AssignPrivateProperty(object entity, string propertyName, object? value)
    {
        var property = entity.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)
                       ?? throw new InvalidOperationException(
                           $"La entidad {entity.GetType().Name} no expone la propiedad '{propertyName}'.");

        property.SetValue(entity, value);
    }
}
