using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

public interface IWalletRepository
{
    Task<Wallet?> GetByUserAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga en un solo viaje las billeteras involucradas en un traspaso de garantía.
    /// Traerlas por separado abriría una ventana entre ambas lecturas en la que la segunda
    /// podría verse ya modificada por otra puja.
    /// </summary>
    Task<IReadOnlyList<Wallet>> GetByUsersAsync(
        IReadOnlyCollection<int> userIds,
        CancellationToken cancellationToken = default);
}
