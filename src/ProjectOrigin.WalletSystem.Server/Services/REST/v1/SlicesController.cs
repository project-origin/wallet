using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.HierarchicalDeterministicKeys.Interfaces;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

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
    /// <param name = "bus" ></param>
    /// <param name = "request" >Contains the data </param>
    /// <response code="202">The slice was accepted.</response>
    /// <response code="400">Public key could not be decoded.</response>
    /// <response code="404">Receiver endpoint not found.</response>
    [HttpPost]
    [Route("v1/slices/")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ReceiveResponse>> ReceiveSlice(
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] IHDAlgorithm hdAlgorithm,
        [FromServices] IBus bus,
        [FromBody] ReceiveRequest request
    )
    {
        if (!hdAlgorithm.TryImportHDPublicKey(request.PublicKey, out var publicKey))
            return BadRequest("Invalid public key.");

        var endpoint = await unitOfWork.WalletRepository.GetWalletEndpoint(publicKey);
        if (endpoint == null)
            return NotFound("Endpoint not found for public key.");

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

        await bus.Publish(newSliceCommand);

        return Accepted(new ReceiveResponse());
    }
}

#region Records

public record ReceiveRequest
{

    public required byte[] PublicKey { get; init; }
    public required uint Position { get; init; }
    public required FederatedStreamId CertificateId { get; init; }
    public required uint Quantity { get; init; }
    public required byte[] RandomR { get; init; }
    public required IEnumerable<HashedAttribute> HashedAttributes { get; init; }
}

public record HashedAttribute
{
    public required string Key { get; init; }
    public required string Value { get; init; }
    public required byte[] Salt { get; init; }
}

public record ReceiveResponse { }

#endregion
