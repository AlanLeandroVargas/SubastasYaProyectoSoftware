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
}
