using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web.Resource;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Options;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[Authorize]
[ApiController]
public class WalletController : ControllerBase
{
    /// <summary>
    /// Creates a new wallet for the user.
    /// </summary>
    /// <remarks>
    /// Currently, only **one wallet** per user is supported.
    /// The wallet is created with a private key, which is used to generate sub-public-keys for each certificate-slice.
    /// The private key can be provided, but it is optional, if omittted a random one is generated.
    /// </remarks>
    /// <param name = "unitOfWork" ></param>
    /// <param name = "hdAlgorithm" ></param>
    /// <param name = "serviceOptions" ></param>
    /// <param name = "request" > The private key to import. If not provided, a new private key will be generated.</param>
    /// <response code="201">The wallet was created.</response>
    /// <response code="400">If private key is invalid or if wallet for user already exists.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost]
    [Route("v1/wallets")]
    [RequiredScope("wallet:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateWalletResponse>> CreateWallet(
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] IHDAlgorithm hdAlgorithm,
        [FromServices] IOptions<ServiceOptions> serviceOptions,
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

            unitOfWork.Commit();

            return Created(new Uri(serviceOptions.Value.EndpointAddress, $"/v1/wallets/{newWallet.Id}"), new CreateWalletResponse
            {
                WalletId = newWallet.Id
            });
        }
        catch (Exception ex) when (ex.Message.Contains("duplicate key value violates unique constraint"))
        {
            return BadRequest("Wallet already exists.");
        }
    }

    /// <summary>
    /// Gets all wallets for the user.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <response code="200">The wallets were found.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/wallets")]
    [RequiredScope("wallet:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<WalletRecord>>> GetWallets(
        [FromServices] IUnitOfWork unitOfWork
    )
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var wallet = await unitOfWork.WalletRepository.GetWallet(subject);

        var list = new List<WalletRecord>();
        if (wallet is not null)
            list.Add(new WalletRecord
            {
                Id = wallet.Id,
                PublicKey = wallet.PrivateKey.Neuter(),
            });

        return Ok(new ResultList<WalletRecord>
        {
            Result = list,
            Metadata = new PageInfo
            {
                Count = list.Count,
                Limit = int.MaxValue,
                Offset = 0,
                Total = list.Count,
            }
        });
    }

    /// <summary>
    /// Gets a specific wallet for the user.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="walletId">The ID of the wallet to get.</param>
    /// <response code="200">The wallet was found.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="404">If the wallet specified is not found for the user.</response>
    [HttpGet]
    [Route("v1/wallets/{walletId}")]
    [RequiredScope("wallet:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<WalletRecord>> GetWallet(
        [FromServices] IUnitOfWork unitOfWork,
        [FromRoute] Guid walletId
    )
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var wallet = await unitOfWork.WalletRepository.GetWallet(walletId);

        if (wallet is null || wallet.Owner != subject) return NotFound();

        return Ok(new WalletRecord
        {
            Id = wallet.Id,
            PublicKey = wallet.PrivateKey.Neuter(),
        });
    }

    /// <summary>
    /// Creates a new wallet endpoint on the users wallet, which can be sent to other services to receive certificate-slices.
    /// </summary>
    /// <param name = "unitOfWork" ></param>
    /// <param name = "serviceOptions" ></param>
    /// <param name = "walletId" > The ID of the wallet to create the endpoint on.</param>
    /// <response code="201">The wallet endpoint was created.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="404">If the wallet specified is not found for the user.</response>
    [HttpPost]
    [Route("v1/wallets/{walletId}/endpoints")]
    [RequiredScope("wallet-endpoint:create")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CreateWalletEndpointResponse>> CreateWalletEndpoint(
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] IOptions<ServiceOptions> serviceOptions,
        [FromRoute] Guid walletId
    )
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var wallet = await unitOfWork.WalletRepository.GetWallet(walletId);

        if (wallet is null || wallet.Owner != subject) return NotFound();

        var walletEndpoint = await unitOfWork.WalletRepository.CreateWalletEndpoint(wallet.Id);

        unitOfWork.Commit();

        return Created(null as string, new CreateWalletEndpointResponse
        {
            WalletReference = new WalletEndpointReference
            {
                Version = 1,
                Endpoint = new Uri(serviceOptions.Value.EndpointAddress, $"/v1/slices"),
                PublicKey = walletEndpoint.PublicKey
            }
        });

    }

    /// <summary>
    /// Creates a new external endpoint for the user, which can user can use to send certficates to the other wallet.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="request">The request to create the external endpoint.</param>
    /// <response code="201">The external endpoint was created.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost]
    [Route("v1/external-endpoints")]
    [RequiredScope("external-endpoint:create")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateExternalEndpointResponse>> CreateExternalEndpoint(
        [FromServices] IUnitOfWork unitOfWork,
        [FromBody] CreateExternalEndpointRequest request
    )
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var externalEndpoint = await unitOfWork.WalletRepository.CreateExternalEndpoint(
            subject,
            request.WalletReference.PublicKey,
            request.TextReference,
            request.WalletReference.Endpoint.ToString());

        unitOfWork.Commit();

        return Created(null as string, new CreateExternalEndpointResponse
        {
            ReceiverId = externalEndpoint.Id
        });
    }

}

#region Records

/// <summary>
/// A wallet record
/// </summary>
public record WalletRecord()
{
    public required Guid Id { get; init; }
    public required IHDPublicKey PublicKey { get; init; }
}

/// <summary>
/// Request to create a new wallet.
/// </summary>
public record CreateWalletRequest()
{
    /// <summary>
    /// The private key to import. If not provided, a private key will be generated.
    /// </summary>
    public byte[]? PrivateKey { get; init; }
}

/// <summary>
/// Response to create a new wallet.
/// </summary>
public record CreateWalletResponse()
{
    /// <summary>
    /// The ID of the created wallet.
    /// </summary>
    public Guid WalletId { get; init; }
}

/// <summary>
/// Response to create a new wallet endpoint on the users wallet.
/// </summary>
public record CreateWalletEndpointResponse()
{
    /// <summary>
    /// Reference object to the wallet endpoint created.
    /// Contains the necessary information to send to another wallet to create a link so certificates can be transferred.
    /// </summary>
    public required WalletEndpointReference WalletReference { get; init; }
}

/// <summary>
/// Request to create a new external endpoint.
/// </summary>
public record CreateExternalEndpointRequest()
{
    /// <summary>
    /// The wallet reference to the wallet, one wants to create a link to.
    /// </summary>
    public required WalletEndpointReference WalletReference { get; init; }

    /// <summary>
    /// The text reference for the wallet, one wants to create a link to.
    /// </summary>
    public required string TextReference { get; init; }
}

/// <summary>
/// Response to create a new external endpoint.
/// </summary>
public record CreateExternalEndpointResponse()
{
    /// <summary>
    /// The ID of the created external endpoint.
    /// </summary>
    public required Guid ReceiverId { get; init; }
}

public record WalletEndpointReference()
{
    /// <summary>
    /// The version of the ReceiveSlice API.
    /// </summary>
    public required int Version { get; init; } // The version of the Wallet protobuf API.

    /// <summary>
    /// The url endpoint of where the wallet is hosted.
    /// </summary>
    public required Uri Endpoint { get; init; }

    /// <summary>
    /// The public key used to generate sub-public-keys for each slice.
    /// </summary>
    public required IHDPublicKey PublicKey { get; init; }
}

#endregion
