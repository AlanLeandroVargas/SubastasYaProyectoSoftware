namespace SubastaYa.Application.Dtos;

/// <summary>Credenciales enviadas para abrir una sesión.</summary>
public sealed record SignInRequest(string Email, string Password);

/// <summary>Sesión emitida por la API junto con el perfil público del usuario.</summary>
public sealed record SessionDto(string Token, DateTime ExpiresAtUtc, UserDto User);

/// <summary>Perfil público de un usuario. Nunca expone el hash de la contraseña.</summary>
public sealed record UserDto(int Id, string Email, string Name, string Pseudonym);
