namespace SubastaYa.Application.Abstractions.Persistence;

/// <summary>
/// Frontera transaccional de los casos de uso. Permite que un caso de uso confirme varias
/// escrituras como una sola unidad sin que la capa de aplicación conozca EF Core.
/// </summary>
public interface IUnitOfWork
{
    /// <summary>
    /// Confirma todos los cambios pendientes en una única operación atómica: si alguno falla,
    /// ninguno queda persistido.
    /// </summary>
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
