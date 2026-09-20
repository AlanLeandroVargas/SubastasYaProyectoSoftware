using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SubastaYa.Application.Abstractions.Security;
using SubastaYa.Application.Abstractions.Services;
using SubastaYa.Application.Dtos;

namespace SubastaYa.Api.Controllers;

/// <summary>
/// Billetera virtual del usuario autenticado.
/// El identificador "me" evita exponer los identificadores internos de billeteras ajenas: no hay
/// forma de pedir el saldo de otro escribiendo otro número en la URL.
/// </summary>
[ApiController]
[Route("api/v1/wallets")]
[Authorize]
[Produces("application/json")]
public sealed class WalletsController : ControllerBase
{
    private const int DefaultLedgerSize = 50;

    private readonly IWalletService _wallet;
    private readonly ICurrentUser _currentUser;

    public WalletsController(IWalletService wallet, ICurrentUser currentUser)
    {
        _wallet = wallet;
        _currentUser = currentUser;
    }

    /// <summary>Desglose del saldo total, retenido y disponible.</summary>
    [HttpGet("me")]
    [ProducesResponseType(typeof(BalanceDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<BalanceDto>> GetBalance(CancellationToken cancellationToken) =>
        Ok(await _wallet.GetBalanceAsync(_currentUser.RequireId(), cancellationToken));

    /// <summary>Acredita fondos simulados en la billetera.</summary>
    [HttpPost("me/deposits")]
    [ProducesResponseType(typeof(BalanceDto), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status409Conflict)]
    public async Task<ActionResult<BalanceDto>> Deposit(
        [FromBody] DepositRequest request,
        CancellationToken cancellationToken)
    {
        var balance = await _wallet.CreditAsync(_currentUser.RequireId(), request, cancellationToken);

        return CreatedAtAction(nameof(GetBalance), balance);
    }

    /// <summary>Historial de movimientos de la billetera, del más reciente al más antiguo.</summary>
    [HttpGet("me/transactions")]
    [ProducesResponseType(typeof(IReadOnlyList<LedgerEntryDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<IReadOnlyList<LedgerEntryDto>>> ListLedgerEntries(
        CancellationToken cancellationToken,
        [FromQuery] int count = DefaultLedgerSize) =>
        Ok(await _wallet.GetLedgerAsync(_currentUser.RequireId(), count, cancellationToken));
}
