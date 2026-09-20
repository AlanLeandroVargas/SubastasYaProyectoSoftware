namespace SubastaYa.Application.Dtos;

/// <summary>Criterios de ordenamiento que ofrece el catálogo.</summary>
public enum AuctionSort
{
    /// <summary>Primero las subastas que están por cerrar.</summary>
    EndingSoonest = 1,

    /// <summary>Primero las subastas con la oferta líder más alta.</summary>
    HighestBid = 2,

    /// <summary>Primero las publicaciones más nuevas.</summary>
    Newest = 3
}
