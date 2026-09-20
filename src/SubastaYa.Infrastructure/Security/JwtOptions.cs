namespace SubastaYa.Infrastructure.Security;

/// <summary>Parámetros de emisión y validación de los tokens de acceso.</summary>
public sealed class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; init; } = "SubastaYa";

    public string Audience { get; init; } = "SubastaYa.Frontend";

    /// <summary>
    /// Clave simétrica de firma. En producción debe provenir de un gestor de secretos
    /// (variables de entorno, user-secrets o Azure Key Vault), nunca del appsettings.
    /// </summary>
    public string Key { get; init; } = string.Empty;

    public int LifetimeMinutes { get; init; } = 480;
}
