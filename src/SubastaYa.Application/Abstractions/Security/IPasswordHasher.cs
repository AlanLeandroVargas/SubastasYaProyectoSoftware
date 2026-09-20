namespace SubastaYa.Application.Abstractions.Security;

/// <summary>Deriva y verifica hashes de contraseñas sin exponer el algoritmo a las capas superiores.</summary>
public interface IPasswordHasher
{
    string Hash(string plainPassword);

    bool Verify(string plainPassword, string storedHash);
}
