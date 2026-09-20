using Microsoft.EntityFrameworkCore;
using SubastaYa.Application.Abstractions.Security;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Seeding;

/// <summary>
/// Carga los datos semilla obligatorios de la consigna.
///
/// Se ejecuta al arrancar la aplicación, y no como <c>HasData</c> de una migración, porque los
/// casos de prueba se definen en relación al instante actual: "cierra en 25 minutos", "venció
/// hace 2 minutos". Una semilla estática quedaría obsoleta apenas se genera la migración.
///
/// Todo movimiento de saldo deja su asiento en el libro mayor, igual que en la aplicación real:
/// así el historial de cada billetera explica por completo su saldo desde el primer arranque.
///
/// Los textos del catálogo van en español: son contenido que ve el usuario final.
/// </summary>
internal sealed class DatabaseSeeder
{
    /// <summary>Contraseña compartida por todas las cuentas de prueba.</summary>
    private const string DemoPassword = "Password123!";

    private readonly SubastaYaDbContext _context;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IClock _clock;

    public DatabaseSeeder(SubastaYaDbContext context, IPasswordHasher passwordHasher, IClock clock)
    {
        _context = context;
        _passwordHasher = passwordHasher;
        _clock = clock;
    }

    public async Task SeedAsync(CancellationToken cancellationToken = default)
    {
        if (await _context.Users.AnyAsync(cancellationToken))
        {
            return;
        }

        var now = _clock.UtcNow;
        var categories = await SeedCategoriesAsync(cancellationToken);
        var users = await SeedUsersAndWalletsAsync(now, cancellationToken);

        await SeedAuctionsAsync(users, categories, now, cancellationToken);

        await _context.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<string, Category>> SeedCategoriesAsync(CancellationToken cancellationToken)
    {
        var categories = new[]
        {
            new Category("Tecnología", "bi-cpu"),
            new Category("Coleccionables", "bi-gem"),
            new Category("Indumentaria", "bi-bag-heart"),
            new Category("Vehículos", "bi-car-front")
        };

        _context.Categories.AddRange(categories);
        await _context.SaveChangesAsync(cancellationToken);

        return categories.ToDictionary(category => category.Name);
    }

    /// <summary>
    /// Crea los cuatro usuarios de prueba con sus depósitos iniciales.
    /// Se persisten antes que las subastas porque tanto el replay de las pujas históricas como
    /// los asientos del libro mayor necesitan el identificador definitivo de cada billetera.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, User>> SeedUsersAndWalletsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        var seller = CreateUser("vendedor@test.com", "Valeria Vendedora", "VendedorPro", now);
        var firstBuyer = CreateUser("comprador1@test.com", "Carlos Comprador", "Postor_A1", now);
        var secondBuyer = CreateUser("comprador2@test.com", "Camila Compradora", "Postor_B2", now);
        var brokeBuyer = CreateUser("sinfondos@test.com", "Sergio SinFondos", "Postor_C3", now);

        _context.Users.AddRange(seller, firstBuyer, secondBuyer, brokeBuyer);
        await _context.SaveChangesAsync(cancellationToken);

        // El vendedor arranca sin fondos: su saldo se construye con las ventas adjudicadas.
        Deposit(firstBuyer, 150_000m, now);

        // El segundo comprador incluye la garantía de la subasta vencida que todavía está
        // pendiente de adjudicación, por eso su total supera a los $200.000 disponibles.
        Deposit(secondBuyer, 230_000m, now);
        Deposit(brokeBuyer, 500m, now);

        await _context.SaveChangesAsync(cancellationToken);

        return new Dictionary<string, User>
        {
            ["seller"] = seller,
            ["firstBuyer"] = firstBuyer,
            ["secondBuyer"] = secondBuyer,
            ["brokeBuyer"] = brokeBuyer
        };
    }

    private async Task SeedAuctionsAsync(
        IReadOnlyDictionary<string, User> users,
        IReadOnlyDictionary<string, Category> categories,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var seller = users["seller"];
        var firstBuyer = users["firstBuyer"];
        var secondBuyer = users["secondBuyer"];

        await CreateStandardActiveAuctionAsync(
            seller, firstBuyer, secondBuyer, categories["Tecnología"], now, cancellationToken);
        await CreateCriticalActiveAuctionAsync(seller, categories["Coleccionables"], now, cancellationToken);
        await CreateScheduledAuctionAsync(seller, categories["Indumentaria"], now, cancellationToken);
        await CreateExpiredAuctionWithWinnerAsync(seller, secondBuyer, categories["Vehículos"], now, cancellationToken);
        await CreateExpiredAuctionWithoutBidsAsync(seller, categories["Coleccionables"], now, cancellationToken);
    }

    /// <summary>Caso 1: subasta en curso que cierra en 25 minutos, con dos pujas previas y líder en $45.000.</summary>
    private async Task CreateStandardActiveAuctionAsync(
        User seller,
        User firstBuyer,
        User secondBuyer,
        Category category,
        DateTime now,
        CancellationToken cancellationToken)
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

        await PersistAuctionAsync(auction, cancellationToken);

        PlaceHistoricBid(auction, secondBuyer, 40_000m, now.AddMinutes(-12));
        PlaceHistoricBid(auction, firstBuyer, 45_000m, now.AddMinutes(-4));
    }

    /// <summary>Caso 2: subasta en zona crítica que cierra en 90 segundos, para probar la alerta visual.</summary>
    private async Task CreateCriticalActiveAuctionAsync(
        User seller,
        Category category,
        DateTime now,
        CancellationToken cancellationToken)
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
            endsAt: now.AddSeconds(90),
            now);

        await PersistAuctionAsync(auction, cancellationToken);
    }

    /// <summary>Caso 3: subasta programada para comenzar en 24 horas, con las pujas bloqueadas.</summary>
    private async Task CreateScheduledAuctionAsync(
        User seller,
        Category category,
        DateTime now,
        CancellationToken cancellationToken)
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

        await PersistAuctionAsync(auction, cancellationToken);
    }

    /// <summary>
    /// Caso 4: subasta cuyo cierre ya pasó y tiene puja ganadora.
    /// Queda pendiente de adjudicación hasta que exista el proceso en segundo plano: la garantía
    /// del ganador sigue congelada, que es exactamente el estado que ese proceso deberá resolver.
    /// </summary>
    private async Task CreateExpiredAuctionWithWinnerAsync(
        User seller,
        User winner,
        Category category,
        DateTime now,
        CancellationToken cancellationToken)
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

        await PersistAuctionAsync(auction, cancellationToken);

        PlaceHistoricBid(auction, winner, 30_000m, now.AddHours(-1));

        ForceClosingDate(auction, now.AddMinutes(-2));
    }

    /// <summary>Caso 5: subasta vencida sin ninguna oferta, pendiente de pasar a desierta.</summary>
    private async Task CreateExpiredAuctionWithoutBidsAsync(
        User seller,
        Category category,
        DateTime now,
        CancellationToken cancellationToken)
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

        await PersistAuctionAsync(auction, cancellationToken);

        ForceClosingDate(auction, now.AddMinutes(-5));
    }

    /// <summary>
    /// Inserta la subasta de inmediato: los asientos de las pujas históricas referencian su
    /// identificador, y ése sólo existe una vez persistida la fila.
    /// </summary>
    private async Task PersistAuctionAsync(Auction auction, CancellationToken cancellationToken)
    {
        _context.Auctions.Add(auction);
        await _context.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Crea un usuario de prueba con la contraseña de demostración ya hasheada.</summary>
    private User CreateUser(string email, string name, string pseudonym, DateTime now) =>
        new(email, name, pseudonym, _passwordHasher.Hash(DemoPassword), now);

    /// <summary>Acredita fondos iniciales dejando el asiento que los justifica.</summary>
    private void Deposit(User user, decimal amount, DateTime now)
    {
        user.Wallet.Credit(amount);
        _context.LedgerEntries.Add(LedgerEntry.Deposit(user.Wallet.Id, amount, now));
    }

    /// <summary>
    /// Reproduce una puja histórica respetando las mismas reglas de garantía que aplicará el caso
    /// de uso real: se congela el saldo del nuevo líder y se libera el del anterior, y cada
    /// movimiento queda asentado en el libro mayor de la billetera correspondiente.
    /// </summary>
    private void PlaceHistoricBid(Auction auction, User bidder, decimal amount, DateTime placedAt)
    {
        var outbidBidderId = auction.LeadingBidderId;
        var escrowToRelease = auction.CurrentAmount;

        bidder.Wallet.Hold(amount);
        _context.LedgerEntries.Add(LedgerEntry.Hold(bidder.Wallet.Id, amount, placedAt, auction.Id));

        if (outbidBidderId is int displacedBidderId)
        {
            var displacedWallet = _context.ChangeTracker
                .Entries<Wallet>()
                .Select(entry => entry.Entity)
                .First(wallet => wallet.UserId == displacedBidderId);

            displacedWallet.ReleaseHold(escrowToRelease);
            _context.LedgerEntries.Add(
                LedgerEntry.Release(displacedWallet.Id, escrowToRelease, placedAt, auction.Id));
        }

        auction.PlaceBid(bidder.Id, amount, placedAt);
    }

    /// <summary>
    /// Retrocede la fecha de cierre de una subasta ya construida.
    /// Se hace a través del rastreador de EF y no de la entidad para no abrir en el dominio una
    /// puerta que permita publicar subastas vencidas desde la API.
    /// </summary>
    private void ForceClosingDate(Auction auction, DateTime endsAt) =>
        _context.Entry(auction).Property(entity => entity.EndsAt).CurrentValue = endsAt;
}
