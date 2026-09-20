namespace SubastaYa.Application.Common;

/// <summary>Página de resultados junto con los metadatos que el frontend necesita para paginar.</summary>
public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalItems)
{
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalItems / (double)PageSize);

    public bool HasNextPage => Page < TotalPages;

    public PagedResult<TTarget> Map<TTarget>(Func<T, TTarget> projection) =>
        new(Items.Select(projection).ToList(), Page, PageSize, TotalItems);
}
