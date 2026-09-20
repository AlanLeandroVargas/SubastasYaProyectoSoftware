using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.Services;

public interface IAuctionPublishingService
{
    Task<AuctionDetailDto> PublishAsync(
        CreateAuctionRequest request,
        int sellerId,
        CancellationToken cancellationToken = default);
}
