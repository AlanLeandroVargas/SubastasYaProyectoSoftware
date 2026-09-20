using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Comprueba la existencia sin materializar la categoría entera.</summary>
    Task<bool> ExistsAsync(int id, CancellationToken cancellationToken = default);
}
