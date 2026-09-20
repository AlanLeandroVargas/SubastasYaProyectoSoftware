using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.Services;

public interface IBiddingService
{
    Task<BidResultDto> PlaceBidAsync(
        int auctionId,
        int bidderId,
        PlaceBidRequest request,
        CancellationToken cancellationToken = default);
}
