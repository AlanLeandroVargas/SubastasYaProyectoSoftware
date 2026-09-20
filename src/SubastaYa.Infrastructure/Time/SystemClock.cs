using SubastaYa.Application.Abstractions.Time;

namespace SubastaYa.Infrastructure.Time;

/// <summary>Reloj real del servidor. Siempre en UTC para evitar ambigüedades horarias.</summary>
internal sealed class SystemClock : IClock
{
    public DateTime UtcNow => DateTime.UtcNow;
}
