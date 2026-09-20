using SubastaYa.Application.Abstractions.Persistence;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Abstractions.Time;
using SubastaYa.Application.Dtos;
using SubastaYa.Application.Mapping;
using SubastaYa.Domain.Entities;
using SubastaYa.Domain.Enums;
using SubastaYa.Domain.Exceptions;

namespace SubastaYa.Application.Services;

/// <summary>
/// Publicación de subastas por parte de un vendedor.
/// El servicio sólo orquesta: la coherencia económica y temporal la valida la propia entidad, de
/// modo que no exista una segunda definición de las reglas conviviendo con la del dominio.
/// </summary>
public sealed class AuctionPublishingService : IAuctionPublishingService
{
    private readonly IAuctionRepository _auctions;
    private readonly ICategoryRepository _categories;
    private readonly IAuditRepository _auditRecords;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public AuctionPublishingService(
        IAuctionRepository auctions,
        ICategoryRepository categories,
        IAuditRepository auditRecords,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _auctions = auctions;
        _categories = categories;
        _auditRecords = auditRecords;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<AuctionDetailDto> PublishAsync(
        CreateAuctionRequest request,
        int sellerId,
        CancellationToken cancellationToken = default)
    {
        // La categoría se comprueba acá y no en el dominio: la subasta no puede conocer el
        // catálogo entero, y dejar que falle la clave foránea daría un 500 en lugar de un 400.
        if (!await _categories.ExistsAsync(request.CategoryId, cancellationToken))
        {
            throw new ValidationException($"La categoría {request.CategoryId} no existe.");
        }

        var now = _clock.UtcNow;

        var auction = Auction.Publish(
            sellerId,
            request.CategoryId,
            request.Title,
            request.Description,
            request.ImageUrl,
            request.StartingPrice,
            request.MinimumIncrement,
            NormalizeToUtc(request.StartsAtUtc),
            NormalizeToUtc(request.EndsAtUtc),
            now);

        await _unitOfWork.ExecuteInTransactionAsync<object?>(async token =>
        {
            _auctions.Add(auction);

            // Se persiste primero para que el registro de auditoría pueda referenciar el
            // identificador generado por la base.
            await _unitOfWork.SaveChangesAsync(token);

            _auditRecords.Add(AuditRecordFactory.Create(
                AuditedEntity.Auction,
                auction.Id,
                AuditAction.AuctionCreated,
                sellerId,
                new
                {
                    title = auction.Title,
                    status = auction.Status.ToString().ToUpperInvariant(),
                    startingPrice = auction.StartingPrice,
                    minimumIncrement = auction.MinimumIncrement,
                    startsAtUtc = auction.StartsAt,
                    endsAtUtc = auction.EndsAt
                },
                now));

            await _unitOfWork.SaveChangesAsync(token);

            return null;
        }, cancellationToken);

        // Se relee para devolver el detalle con vendedor y categoría resueltos, igual que lo
        // haría un GET posterior: el cliente recibe exactamente la misma forma de recurso.
        var published = await _auctions.GetDetailAsync(auction.Id, cancellationToken)
                        ?? throw new ResourceNotFoundException("la subasta", auction.Id);

        return published.ToDetailDto(sellerId);
    }

    /// <summary>
    /// El frontend envía instantes en UTC. Se normaliza el <c>Kind</c> para que las comparaciones
    /// contra el reloj del servidor y los valores almacenados sean siempre homogéneas: un
    /// <c>DateTime</c> sin zona se interpretaría como hora local y correría la subasta.
    /// </summary>
    private static DateTime NormalizeToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
