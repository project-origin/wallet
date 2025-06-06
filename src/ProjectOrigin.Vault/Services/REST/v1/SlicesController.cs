using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;

namespace ProjectOrigin.Vault.Services.REST.v1;

[AllowAnonymous]
[ApiController]
public class SlicesController : ControllerBase
{
    /// <summary>
    /// Receive a certificate-slice from another wallet.
    /// </summary>
    /// <remarks>
    /// This request is used to receive a certificate-slice from another wallet, which is then stored in the local wallet.
    /// The endpoint is verified to exists within the wallet system, otherwise a 404 will be returned.
    /// The endpoint will return 202 Accepted was initial validation has succeeded.
    /// The certificate-slice will further verified with data from the registry in a seperate thread.
    /// </remarks>
    /// <param name = "unitOfWork" ></param>
    /// <param name = "hdAlgorithm" ></param>
    /// <param name = "request" >Contains the data </param>
    /// <response code="202">The slice was accepted.</response>
    /// <response code="400">Public key could not be decoded or if the wallet is disabled.</response>
    /// <response code="404">Receiver endpoint not found or wallet not found for the user.</response>
    [HttpPost]
    [Route("v1/slices")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceiveResponse>> ReceiveSlice(
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] IHDAlgorithm hdAlgorithm,
        [FromBody] ReceiveRequest request
    )
    {
        if (!hdAlgorithm.TryImportHDPublicKey(request.PublicKey, out var publicKey))
            return BadRequest("Invalid public key.");

        var endpoint = await unitOfWork.WalletRepository.GetWalletEndpoint(publicKey);
        if (endpoint == null)
            return NotFound("Endpoint not found for public key.");

        var wallet = await unitOfWork.WalletRepository.GetWallet(endpoint.WalletId);
        if (wallet == null) return NotFound("You don't own a wallet. Create a wallet first.");
        if (wallet.IsDisabled()) return BadRequest("Unable to interact with a disabled wallet.");

        var newSliceCommand = new VerifySliceCommand
        {
            Id = Guid.NewGuid(),
            WalletId = endpoint.WalletId,
            WalletEndpointId = endpoint.Id,
            WalletEndpointPosition = (int)request.Position,
            Registry = request.CertificateId.Registry,
            CertificateId = request.CertificateId.StreamId,
            Quantity = request.Quantity,
            RandomR = request.RandomR,
            HashedAttributes = request.HashedAttributes.Select(x => new WalletAttribute
            {
                CertificateId = request.CertificateId.StreamId,
                RegistryName = request.CertificateId.Registry,
                Key = x.Key,
                Value = x.Value,
                Salt = x.Salt
            }).ToList()
        };

        await unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
        {
            Created = DateTimeOffset.UtcNow.ToUtcTime(),
            Id = Guid.NewGuid(),
            MessageType = typeof(VerifySliceCommand).ToString(),
            JsonPayload = JsonSerializer.Serialize(newSliceCommand)
        });
        unitOfWork.Commit();

        return Accepted(new ReceiveResponse());
    }
}

#region Records

/// <summary>
/// Request to receive a certificate-slice from another wallet.
/// </summary>
public record ReceiveRequest()
{
    /// <summary>
    /// The public key of the receiving wallet.
    /// </summary>
    public required byte[] PublicKey { get; init; }

    /// <summary>
    /// The sub-position of the publicKey used on the slice on the registry.
    /// </summary>
    public required uint Position { get; init; }

    /// <summary>
    /// The id of the certificate.
    /// </summary>
    public required FederatedStreamId CertificateId { get; init; }

    /// <summary>
    /// The quantity of the slice.
    /// </summary>
    public required uint Quantity { get; init; }

    /// <summary>
    /// The random R used to generate the pedersen commitment with the quantitiy.
    /// </summary>
    public required byte[] RandomR { get; init; }

    /// <summary>
    /// List of hashed attributes, their values and salts so the receiver can access the data.
    /// </summary>
    public required IEnumerable<HashedAttribute> HashedAttributes { get; init; }
}

/// <summary>
/// Hashed attribute with salt.
/// </summary>
public record HashedAttribute()
{

    /// <summary>
    /// The key of the attribute.
    /// </summary>
    public required string Key { get; init; }

    /// <summary>
    /// The value of the attribute.
    /// </summary>
    public required string Value { get; init; }

    /// <summary>
    /// The salt used to hash the attribute.
    /// </summary>
    public required byte[] Salt { get; init; }
}

/// <summary>
/// Response to receive a certificate-slice from another wallet.
/// </summary>
public record ReceiveResponse() { }

#endregion
