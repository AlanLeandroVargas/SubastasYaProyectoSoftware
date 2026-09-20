using Microsoft.EntityFrameworkCore;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Domain.Exceptions;

namespace SubastaYa.Infrastructure.Persistence;

/// <summary>
/// Implementación de la frontera transaccional sobre EF Core.
/// Un guardado agrupa todos los cambios pendientes del contexto en una transacción implícita, de
/// modo que saldo y asiento se confirman o se descartan juntos.
/// Además traduce el fallo de bloqueo optimista a una excepción del dominio, para que la capa de
/// aplicación pueda reaccionar al conflicto sin depender del ORM.
/// </summary>
internal sealed class UnitOfWork : IUnitOfWork
{
    private readonly SubastaYaDbContext _context;

    public UnitOfWork(SubastaYaDbContext context)
    {
        _context = context;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await _context.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new ConcurrencyConflictException(
                "Otra operación modificó el mismo registro mientras se procesaba esta petición. Es necesario reintentar.");
        }
    }

    public async Task<TResult> ExecuteInTransactionAsync<TResult>(
        Func<CancellationToken, Task<TResult>> operation,
        CancellationToken cancellationToken = default)
    {
        // Si ya hay una transacción abierta se la reutiliza: el caso de uso externo es el dueño
        // de la frontera transaccional y decide cuándo confirmar.
        if (_context.Database.CurrentTransaction is not null)
        {
            return await operation(cancellationToken);
        }

        await using var transaction = await _context.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var result = await operation(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return result;
        }
        catch
        {
            // Reversión completa: saldos, pujas, libro mayor y auditoría vuelven a su estado previo.
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    /// <summary>
    /// Vacía el rastreador de cambios. Después de una reversión el contexto conserva en memoria
    /// entidades modificadas que ya no se corresponden con la base; reutilizarlo sin limpiarlo
    /// haría que el siguiente guardado intentara reaplicarlas.
    /// </summary>
    public void DiscardPendingChanges() => _context.ChangeTracker.Clear();
}
