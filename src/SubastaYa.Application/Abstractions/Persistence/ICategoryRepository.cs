using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

public interface ICategoryRepository
{
    Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default);
}
