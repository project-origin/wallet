using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[Authorize]
[ApiController]
public class WalletController : ControllerBase
{

    /// <summary>
    /// Creates a new wallet for the user.
    /// Currently, only one wallet per user is supported.
    /// </summary>
    /// <param name = "unitOfWork" ></param>
    /// <param name = "hdAlgorithm" ></param>
    /// <param name = "request" > The private key to import. If not provided, a new private key will be generated.</param>
    /// <response code="200">The wallet was created.</response>
    /// <response code="400">If private key is invalid.</response>
    /// <response code="400">If wallet for user already exists.</response>
    /// <response code="401">If the user is not authenticated.</response>
    ///
    [HttpPost]
    [Route("v1/wallets")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateWalletResponse>> CreateWallet(
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] IHDAlgorithm hdAlgorithm,
        [FromBody] CreateWalletRequest request
    )
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        IHDPrivateKey hdPrivateKey;

        if (request.PrivateKey is not null)
        {
            if (!hdAlgorithm.TryImportHDPrivateKey(request.PrivateKey, out hdPrivateKey))
                return BadRequest("Invalid private key.");
        }
        else
            hdPrivateKey = hdAlgorithm.GenerateNewPrivateKey();

        try
        {
            var newWallet = new Wallet
            {
                Id = Guid.NewGuid(),
                Owner = subject,
                PrivateKey = hdPrivateKey
            };

            await unitOfWork.WalletRepository.Create(newWallet);

            return Created((string?)null, new CreateWalletResponse
            {
                WalletId = newWallet.Id
            });
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate key value violates unique constraint"))
        {
            return BadRequest("Wallet already exists.");
        }
    }

}

/// <summary>
/// Request to create a new wallet.
/// </summary>
public record CreateWalletRequest
{
    /// <summary>
    /// The private key to import. If not provided, a private key will be generated.
    /// </summary>
    public byte[]? PrivateKey { get; init; }
}

/// <summary>
/// Response to create a new wallet.
/// </summary>
public record CreateWalletResponse
{
    /// <summary>
    /// The ID of the created wallet.
    /// </summary>
    public Guid WalletId { get; init; }
}
