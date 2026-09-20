using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;

namespace SubastaYa.Application.Services;

/// <summary>
/// Arma el panel "mis actividades" con las participaciones y las publicaciones del usuario.
/// Es sólo lectura y no necesita transacción: proyecta lo que ya escribieron los casos de uso de
/// pujas y de cierre.
/// </summary>
public sealed class UserActivityService : IUserActivityService
{
    private readonly IAuctionRepository _auctions;

    public UserActivityService(IAuctionRepository auctions)
    {
        _auctions = auctions;
    }

    public async Task<IReadOnlyList<ParticipationDto>> GetMyParticipationsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var auctions = await _auctions.GetParticipatedByAsync(userId, cancellationToken);

        return auctions.Select(auction => ToParticipationDto(auction, userId)).ToList();
    }

    public async Task<IReadOnlyList<PublicationDto>> GetMyPublicationsAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var auctions = await _auctions.GetPublishedByAsync(userId, cancellationToken);

        return auctions.Select(ToPublicationDto).ToList();
    }

    private static ParticipationDto ToParticipationDto(Auction auction, int userId)
    {
        // El repositorio filtra el historial: sólo vienen cargadas las ofertas de este usuario,
        // así que el máximo de la colección es su mayor oferta y no la de la subasta.
        var myHighestBid = auction.Bids.Count == 0
            ? decimal.Zero
            : auction.Bids.Max(bid => bid.Amount);

        var isLeader = auction.LeadingBidderId == userId;

        return new ParticipationDto(
            auction.Id,
            auction.Title,
            auction.ImageUrl,
            auction.Status.ToString().ToUpperInvariant(),
            myHighestBid,
            auction.CurrentAmount,
            auction.BidCount,

            // Liderar sólo significa algo mientras la subasta sigue viva; una vez cerrada, lo
            // que corresponde decir es si ganó.
            isLeader && auction.Status == AuctionStatus.Active,
            isLeader && auction.Status == AuctionStatus.Completed,
            auction.EndsAt);
    }

    private static PublicationDto ToPublicationDto(Auction auction)
    {
        // Sólo cuenta como recaudación lo que el proceso de cierre efectivamente liquidó: una
        // subasta con ofertas pero todavía abierta no recaudó nada.
        var revenue = auction.Status == AuctionStatus.Completed ? auction.CurrentAmount : decimal.Zero;

        return new PublicationDto(
            auction.Id,
            auction.Title,
            auction.ImageUrl,
            auction.Status.ToString().ToUpperInvariant(),
            auction.StartingPrice,
            auction.CurrentAmount,
            auction.BidCount,
            revenue,
            auction.Status == AuctionStatus.Completed ? auction.LeadingBidder?.Pseudonym : null,
            auction.EndsAt);
    }
}
