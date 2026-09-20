using SubastaYa.Application.Dtos;

namespace SubastaYa.Application.Abstractions.Services;

public interface IAuthenticationService
{
    Task<SessionDto> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default);

    Task<UserDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default);
}
