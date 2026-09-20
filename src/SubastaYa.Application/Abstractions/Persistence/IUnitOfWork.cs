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

    /// <summary>
    /// Ejecuta la operación dentro de una transacción explícita: confirma al terminar y revierte
    /// por completo ante cualquier error, incluido un conflicto de concurrencia.
    ///
    /// Hace falta cuando el caso de uso necesita <b>leer</b> y <b>escribir</b> de forma
    /// entrelazada; un guardado suelto ya es atómico por sí mismo y no la requiere.
    /// </summary>
    Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Olvida los cambios que quedaron en memoria después de una reversión, para poder reutilizar
    /// el contexto (por ejemplo, para dejar asentado en la auditoría el intento fallido).
    /// </summary>
    void DiscardPendingChanges();
}
