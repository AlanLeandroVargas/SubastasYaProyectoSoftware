using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Security;

/// <summary>Emite las credenciales de acceso con las que el frontend consume la API.</summary>
public interface ITokenGenerator
{
    (string Token, DateTime ExpiresAtUtc) Generate(User user);
}
