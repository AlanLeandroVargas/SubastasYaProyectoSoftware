using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using SubastaYa.Application.Abstractions.Security;

namespace SubastaYa.Api.Security;

/// <summary>Resuelve la identidad del solicitante a partir de las afirmaciones del token JWT.</summary>
internal sealed class HttpCurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public HttpCurrentUser(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public int? Id
    {
        get
        {
            // Según la configuración de mapeo de claims, el identificador puede llegar como "sub"
            // o como NameIdentifier; se contemplan ambos para no depender de ese detalle.
            var claim = _httpContextAccessor.HttpContext?.User.FindFirst(JwtRegisteredClaimNames.Sub)
                        ?? _httpContextAccessor.HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier);

            return int.TryParse(claim?.Value, out var identifier) ? identifier : null;
        }
    }

    public bool IsAuthenticated => Id is not null;
}
