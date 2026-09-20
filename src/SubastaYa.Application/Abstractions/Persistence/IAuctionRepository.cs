using SubastaYa.Application.Common;
using SubastaYa.Application.Dtos;
using SubastaYa.Domain.Entities;

namespace SubastaYa.Application.Abstractions.Persistence;

/// <summary>
/// Puerto de acceso a datos del agregado Auction.
/// </summary>
public interface IAuctionRepository
{
    /// <summary>Búsqueda paginada del catálogo con filtros de estado, categoría, precio y orden.</summary>
    Task<PagedResult<Auction>> SearchAsync(AuctionFilter filter, CancellationToken cancellationToken = default);

    /// <summary>Detalle de lectura con vendedor, categoría e historial de pujas.</summary>
    Task<Auction?> GetDetailAsync(int id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Carga la subasta con seguimiento de cambios para poder modificarla. No trae el historial
    /// completo de ofertas: el agregado ya conserva el importe líder y la cantidad de pujas, y
    /// materializar el historial entero en cada oferta sería un costo inútil.
    /// </summary>
    Task<Auction?> GetForUpdateAsync(int id, CancellationToken cancellationToken = default);
}
