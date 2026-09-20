namespace SubastaYa.Application.Abstractions.Time;

/// <summary>
/// Abstrae el reloj del sistema. Las reglas sensibles al tiempo (anti-sniping, vencimientos)
/// dependen de esta interfaz para poder verificarse sin esperar tiempo real.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
