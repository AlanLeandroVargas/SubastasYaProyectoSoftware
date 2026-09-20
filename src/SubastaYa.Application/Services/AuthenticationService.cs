using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.Security;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Exceptions;

namespace SubastaYa.Application.Services;

/// <summary>Verifica credenciales y emite la sesión con la que el frontend consume la API.</summary>
public sealed class AuthenticationService : IAuthenticationService
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenGenerator _tokenGenerator;

    public AuthenticationService(
        IUserRepository users,
        IPasswordHasher passwordHasher,
        ITokenGenerator tokenGenerator)
    {
        _users = users;
        _passwordHasher = passwordHasher;
        _tokenGenerator = tokenGenerator;
    }

    public async Task<SessionDto> SignInAsync(SignInRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new ValidationException("El email y la contraseña son obligatorios.");
        }

        var user = await _users.GetByEmailAsync(request.Email, cancellationToken);

        // Se responde igual ante usuario inexistente o contraseña incorrecta para no revelar qué
        // direcciones están registradas en la plataforma.
        if (user is null || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            throw new InvalidCredentialsException();
        }

        var (token, expiresAt) = _tokenGenerator.Generate(user);

        return new SessionDto(token, expiresAt, ToDto(user));
    }

    public async Task<UserDto> GetProfileAsync(int userId, CancellationToken cancellationToken = default)
    {
        var user = await _users.GetByIdAsync(userId, cancellationToken)
                   ?? throw new ResourceNotFoundException("el usuario", userId);

        return ToDto(user);
    }

    private static UserDto ToDto(User user) => new(user.Id, user.Email, user.Name, user.Pseudonym);
}
