using System.Security.Cryptography;
using SubastaYa.Application.Abstractions.Security;

namespace SubastaYa.Infrastructure.Security;

/// <summary>
/// Deriva contraseñas con PBKDF2-SHA256 y sal aleatoria por usuario.
/// El formato almacenado es "iteraciones.sal.hash" en base64, autocontenido para poder subir el
/// costo de derivación en el futuro sin invalidar los hashes existentes.
/// </summary>
internal sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int CurrentIterations = 120_000;
    private const int SaltSizeInBytes = 16;
    private const int HashSizeInBytes = 32;
    private const char Separator = '.';

    public string Hash(string plainPassword)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSizeInBytes);
        var hash = Derive(plainPassword, salt, CurrentIterations);

        return string.Join(Separator, CurrentIterations, Convert.ToBase64String(salt), Convert.ToBase64String(hash));
    }

    public bool Verify(string plainPassword, string storedHash)
    {
        var parts = storedHash.Split(Separator);

        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        var salt = Convert.FromBase64String(parts[1]);
        var expectedHash = Convert.FromBase64String(parts[2]);
        var computedHash = Derive(plainPassword, salt, iterations);

        // Comparación en tiempo constante para no filtrar información por temporización.
        return CryptographicOperations.FixedTimeEquals(computedHash, expectedHash);
    }

    private static byte[] Derive(string plainPassword, byte[] salt, int iterations) =>
        Rfc2898DeriveBytes.Pbkdf2(plainPassword, salt, iterations, HashAlgorithmName.SHA256, HashSizeInBytes);
}
