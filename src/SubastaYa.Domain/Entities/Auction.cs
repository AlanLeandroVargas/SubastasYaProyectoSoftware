using SubastaYa.Domain.Enums;
using SubastaYa.Domain.Exceptions;
using SubastaYa.Domain.Results;
using SubastaYa.Domain.Rules;

namespace SubastaYa.Domain.Entities;

/// <summary>
/// Agregado central del sistema. Concentra las reglas de admisión de pujas, el cálculo del
/// próximo monto válido, la extensión anti-sniping y las transiciones de estado, de manera que
/// ningún controlador ni servicio pueda saltearse una validación del negocio.
/// </summary>
public class Auction
{
    private readonly List<Bid> _bids = new();

    private Auction()
    {
        // Constructor requerido por EF Core para la materialización.
        Title = string.Empty;
        Description = string.Empty;
        ImageUrl = string.Empty;
        Seller = null!;
        Category = null!;
        Version = Array.Empty<byte>();
    }

    private Auction(
        int sellerId,
        int categoryId,
        string title,
        string description,
        string imageUrl,
        decimal startingPrice,
        decimal minimumIncrement,
        DateTime startsAt,
        DateTime endsAt,
        DateTime now)
    {
        SellerId = sellerId;
        CategoryId = categoryId;
        Title = title.Trim();
        Description = description.Trim();
        ImageUrl = imageUrl.Trim();
        StartingPrice = startingPrice;
        MinimumIncrement = minimumIncrement;
        StartsAt = startsAt;
        EndsAt = endsAt;
        CurrentAmount = startingPrice;
        Status = startsAt <= now ? AuctionStatus.Active : AuctionStatus.Scheduled;
        Seller = null!;
        Category = null!;
        Version = Array.Empty<byte>();
    }

    public int Id { get; private set; }

    public int SellerId { get; private set; }

    public User Seller { get; private set; }

    public int CategoryId { get; private set; }

    public Category Category { get; private set; }

    public string Title { get; private set; }

    public string Description { get; private set; }

    public string ImageUrl { get; private set; }

    /// <summary>Monto de salida por debajo del cual no se admite ninguna oferta.</summary>
    public decimal StartingPrice { get; private set; }

    /// <summary>Salto mínimo que debe superar cada nueva puja respecto de la oferta líder.</summary>
    public decimal MinimumIncrement { get; private set; }

    public DateTime StartsAt { get; private set; }

    /// <summary>Cierre previsto. La regla anti-sniping puede desplazarlo hacia adelante.</summary>
    public DateTime EndsAt { get; private set; }

    public AuctionStatus Status { get; private set; }

    /// <summary>
    /// Oferta líder vigente (o el precio base mientras no haya ofertas). Se mantiene en el
    /// agregado para que la validación del incremento mínimo y el cambio de líder ocurran bajo
    /// el mismo token de concurrencia que protege a la subasta.
    /// </summary>
    public decimal CurrentAmount { get; private set; }

    public int? LeadingBidderId { get; private set; }

    public User? LeadingBidder { get; private set; }

    public int BidCount { get; private set; }

    /// <summary>
    /// Token de concurrencia optimista. La capa de persistencia lo mapea como rowversion para
    /// que dos pujas simultáneas no puedan registrarse sobre la misma versión de la fila.
    /// </summary>
    public byte[] Version { get; private set; }

    public IReadOnlyCollection<Bid> Bids => _bids.AsReadOnly();

    /// <summary>Monto mínimo que debe alcanzar la próxima oferta para ser aceptada.</summary>
    public decimal MinimumNextBid => BidCount == 0
        ? StartingPrice
        : CurrentAmount + MinimumIncrement;

    public bool HasBids => BidCount > 0;

    /// <summary>Crea una subasta validando la coherencia económica y temporal de la publicación.</summary>
    public static Auction Publish(
        int sellerId,
        int categoryId,
        string title,
        string description,
        string imageUrl,
        decimal startingPrice,
        decimal minimumIncrement,
        DateTime startsAt,
        DateTime endsAt,
        DateTime now)
    {
        ValidateRequiredText(title, "título", maximumLength: 150);
        ValidateRequiredText(description, "descripción", maximumLength: 2000);
        ValidateRequiredText(imageUrl, "url de imagen", maximumLength: 500);
        ValidatePricing(startingPrice, minimumIncrement);
        ValidateSchedule(startsAt, endsAt, now);

        return new Auction(
            sellerId,
            categoryId,
            title,
            description,
            imageUrl,
            startingPrice,
            minimumIncrement,
            startsAt,
            endsAt,
            now);
    }

    /// <summary>
    /// Verifica las condiciones de admisión de una oferta sin modificar el agregado.
    /// El caso de uso la invoca antes de tocar las billeteras para respetar el orden de
    /// validación del negocio: primero el estado, después el monto y recién entonces el saldo.
    /// </summary>
    public void EnsureBidIsAdmissible(int bidderId, decimal amount, DateTime now)
    {
        EnsureIsAcceptingBids(now);
        EnsureBidderIsEligible(bidderId);
        EnsureAmountMeetsMinimumIncrement(amount);
    }

    /// <summary>
    /// Registra una oferta válida y devuelve sus consecuencias económicas y temporales.
    /// No mueve dinero: la liberación y el congelamiento de saldos son responsabilidad del caso
    /// de uso, que los ejecuta dentro de la misma transacción atómica.
    /// Revalida las condiciones de admisión porque la entidad no delega sus invariantes en el llamador.
    /// </summary>
    public BidPlacementResult PlaceBid(int bidderId, decimal amount, DateTime now)
    {
        EnsureBidIsAdmissible(bidderId, amount, now);

        var outbidBidderId = LeadingBidderId;
        var releasedAmount = outbidBidderId is null ? decimal.Zero : CurrentAmount;
        var previousEndsAt = EndsAt;

        var bid = new Bid(this, bidderId, amount, now);
        _bids.Add(bid);

        LeadingBidderId = bidderId;
        CurrentAmount = amount;
        BidCount++;

        var wasExtended = ApplyAntiSnipingExtension(now);

        return new BidPlacementResult(bid, outbidBidderId, releasedAmount, wasExtended, previousEndsAt);
    }

    /// <summary>
    /// Pasa de PROGRAMADA a ACTIVA cuando llega la fecha de inicio.
    /// Lo invoca el proceso en segundo plano, única autoridad sobre el paso del tiempo.
    /// </summary>
    public bool ActivateIfDue(DateTime now)
    {
        if (Status != AuctionStatus.Scheduled || now < StartsAt)
        {
            return false;
        }

        Status = AuctionStatus.Active;
        return true;
    }

    /// <summary>Indica si la ventana temporal expiró y la subasta sigue pendiente de cierre.</summary>
    public bool HasExpired(DateTime now) => Status == AuctionStatus.Active && now >= EndsAt;

    /// <summary>Cierra la subasta adjudicándola al postor líder.</summary>
    public void AwardToLeadingBidder()
    {
        if (!HasBids)
        {
            throw new StateConflictException(
                $"La subasta {Id} no puede adjudicarse porque no recibió ninguna puja.");
        }

        Status = AuctionStatus.Completed;
    }

    /// <summary>Cierra la subasta sin ganador porque venció sin recibir ofertas.</summary>
    public void DeclareAsUnsold()
    {
        if (HasBids)
        {
            throw new StateConflictException(
                $"La subasta {Id} no puede declararse desierta porque tiene {BidCount} pujas.");
        }

        Status = AuctionStatus.Unsold;
    }

    /// <summary>
    /// Regla anti-sniping: una oferta dentro de la ventana crítica desplaza el cierre para que
    /// el resto de los postores alcance a reaccionar.
    /// </summary>
    private bool ApplyAntiSnipingExtension(DateTime now)
    {
        var remainingTime = EndsAt - now;

        if (remainingTime > AuctionRules.AntiSnipingWindow)
        {
            return false;
        }

        EndsAt = EndsAt.Add(AuctionRules.AntiSnipingExtension);
        return true;
    }

    private void EnsureIsAcceptingBids(DateTime now)
    {
        if (Status == AuctionStatus.Scheduled)
        {
            throw new StateConflictException(
                $"La subasta aún no comenzó: las ofertas se habilitan el {StartsAt:dd/MM/yyyy HH:mm} UTC.");
        }

        if (Status != AuctionStatus.Active)
        {
            throw new StateConflictException("La subasta ya se encuentra cerrada y no admite nuevas ofertas.");
        }

        if (now >= EndsAt)
        {
            throw new StateConflictException("El tiempo de la subasta expiró y está pendiente de adjudicación.");
        }
    }

    private void EnsureBidderIsEligible(int bidderId)
    {
        if (bidderId == SellerId)
        {
            throw new AuthorizationException("Un vendedor no puede ofertar en su propia subasta.");
        }

        if (bidderId == LeadingBidderId)
        {
            throw new StateConflictException("Ya sos el postor líder de esta subasta.");
        }
    }

    private void EnsureAmountMeetsMinimumIncrement(decimal amount)
    {
        if (amount < MinimumNextBid)
        {
            throw new ValidationException(
                $"La oferta debe ser de al menos {MinimumNextBid:N2} (oferta líder {CurrentAmount:N2} más incremento mínimo {MinimumIncrement:N2}).");
        }
    }

    private static void ValidateRequiredText(string value, string fieldLabel, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException($"El campo '{fieldLabel}' es obligatorio.");
        }

        if (value.Trim().Length > maximumLength)
        {
            throw new ValidationException($"El campo '{fieldLabel}' no puede superar los {maximumLength} caracteres.");
        }
    }

    private static void ValidatePricing(decimal startingPrice, decimal minimumIncrement)
    {
        if (startingPrice <= decimal.Zero)
        {
            throw new ValidationException("El precio base debe ser mayor a cero.");
        }

        if (minimumIncrement <= decimal.Zero)
        {
            throw new ValidationException("El incremento mínimo debe ser mayor a cero.");
        }

        if (minimumIncrement > startingPrice)
        {
            throw new ValidationException("El incremento mínimo no puede superar al precio base de la subasta.");
        }
    }

    private static void ValidateSchedule(DateTime startsAt, DateTime endsAt, DateTime now)
    {
        if (endsAt <= startsAt)
        {
            throw new ValidationException("La fecha de finalización debe ser posterior a la fecha de inicio.");
        }

        if (endsAt - startsAt < AuctionRules.MinimumDuration)
        {
            throw new ValidationException(
                $"La subasta debe durar al menos {AuctionRules.MinimumDuration.TotalMinutes:N0} minuto(s).");
        }

        if (endsAt <= now)
        {
            throw new ValidationException("No es posible publicar una subasta cuyo cierre ya ocurrió.");
        }
    }
}
