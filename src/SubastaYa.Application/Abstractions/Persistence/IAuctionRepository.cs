using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

/// <summary>
/// Puerto de acceso a datos del agregado Auction.
/// Por ahora sólo declara las operaciones de lectura que necesita el catálogo; las de escritura
/// se irán agregando con las funcionalidades que las requieran.
/// </summary>
public interface IAuctionRepository
{
    /// <summary>Búsqueda paginada del catálogo con filtros de estado, categoría, precio y orden.</summary>
    Task<PagedResult<Auction>> SearchAsync(AuctionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Detalle de lectura con vendedor, categoría e historial de pujas.</summary>
    Task<Auction?> GetDetailAsync(int id, CancellationToken cancellationToken = default);
}
