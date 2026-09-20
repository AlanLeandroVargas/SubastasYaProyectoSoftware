using Microsoft.AspNetCore.SignalR;

namespace SubastaYa.Api.Hubs;

/// <summary>
/// Canal WebSocket de la sala de subasta.
///
/// Cada subasta tiene su propio grupo: una puja se difunde únicamente a los clientes que están
/// mirando esa sala, y no a todos los conectados. Sin esa separación, quien abriera el catálogo
/// recibiría el tráfico de todas las subastas activas a la vez.
///
/// No exige autenticación, en coherencia con el resto de la lectura: el catálogo y el historial
/// de ofertas son públicos, y mirar una sala no debería pedir más permisos que verla por HTTP.
/// Para <b>pujar</b> sigue haciendo falta el token, porque eso pasa por la API REST.
/// </summary>
public sealed class AuctionHub : Hub
{
    public const string Path = "/hubs/auctions";

    /// <summary>Nombre del evento que reciben los clientes cuando se acepta una oferta.</summary>
    public const string BidPlacedEvent = "BidPlaced";

    /// <summary>Nombre del evento que reciben los clientes cuando se cierra la subasta.</summary>
    public const string AuctionClosedEvent = "AuctionClosed";

    public static string GroupName(int auctionId) => $"auction-{auctionId}";

    public Task JoinRoom(int auctionId) =>
        Groups.AddToGroupAsync(Context.ConnectionId, GroupName(auctionId));

    public Task LeaveRoom(int auctionId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(auctionId));
}
