using SubastaYa.Domain.Enums;

namespace SubastaYa.Application.Dtos;

/// <summary>Criterio de búsqueda del catálogo. Viaja desde el controlador hasta el repositorio.</summary>
public sealed class AuctionFilter
{
    private const int MaximumPageSize = 48;

    public AuctionStatus? Status { get; init; }

    public int? CategoryId { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public string? Search { get; init; }

    public AuctionSort Sort { get; init; } = AuctionSort.EndingSoonest;

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 12;

    /// <summary>Normaliza los valores de paginado para blindar al repositorio de entradas inválidas.</summary>
    public AuctionFilter Normalized() => new()
    {
        Status = Status,
        CategoryId = CategoryId,
        MinPrice = MinPrice,
        MaxPrice = MaxPrice,
        Search = string.IsNullOrWhiteSpace(Search) ? null : Search.Trim(),
        Sort = Sort,
        Page = Page < 1 ? 1 : Page,
        PageSize = Math.Clamp(PageSize, 1, MaximumPageSize)
    };
}
