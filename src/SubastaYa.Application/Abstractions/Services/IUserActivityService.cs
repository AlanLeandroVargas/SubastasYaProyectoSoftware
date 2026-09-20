using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.Services;

public interface IUserActivityService
{
    Task<IReadOnlyList<ParticipationDto>> GetMyParticipationsAsync(
        int userId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PublicationDto>> GetMyPublicationsAsync(
        int userId,
        CancellationToken cancellationToken = default);
}
