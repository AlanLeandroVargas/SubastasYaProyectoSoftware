using Microsoft.EntityFrameworkCore;
using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.Repositories;

internal sealed class WalletRepository : IWalletRepository
{
    private readonly SubastaYaDbContext _context;

    public WalletRepository(SubastaYaDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Se devuelve con seguimiento de cambios a propósito: toda lectura de billetera antecede a
    /// un movimiento de saldo que debe quedar protegido por el bloqueo optimista de la fila.
    /// </summary>
    public async Task<Wallet?> GetByUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await _context.Wallets
            .FirstOrDefaultAsync(wallet => wallet.UserId == userId, cancellationToken);

    /// <summary>
    /// Incluye al usuario porque la difusión del evento necesita su seudónimo, y resolverlo
    /// después obligaría a una segunda consulta dentro de la misma transacción.
    /// </summary>
    public async Task<IReadOnlyList<Wallet>> GetByUsersAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default) =>
        await _context.Wallets
            .Include(wallet => wallet.User)
            .Where(wallet => userIds.Contains(wallet.UserId))
            .ToListAsync(cancellationToken);
}
