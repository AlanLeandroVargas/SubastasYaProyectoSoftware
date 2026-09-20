using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Infrastructure.Persistence.InMemory;

/// <summary>Implementación en memoria del repositorio de categorías.</summary>
internal sealed class InMemoryCategoryRepository : ICategoryRepository
{
    private readonly InMemoryCatalogStore _store;

    public InMemoryCategoryRepository(InMemoryCatalogStore store)
    {
        _store = store;
    }

    public Task<IReadOnlyList<Category>> ListAsync(CancellationToken cancellationToken = default)
    {
        IReadOnlyList<Category> categories = _store.Categories
            .OrderBy(category => category.Name)
            .ToList();

        return Task.FromResult(categories);
    }
}
