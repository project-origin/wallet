using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[Authorize]
[ApiController]
public class TransfersController : ControllerBase
{
    /// <summary>
    /// Gets detailed list of all of the transfers that have been made to other wallets.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <param name="skip">The number of items to skip.</param>
    /// <param name="limit">The number of items to return.</param>
    /// <response code="200">Returns the individual transferes within the filter.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/transfers")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<Transfer>>> GetTransfers(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] long? start,
        [FromQuery] long? end,
        [FromQuery] int? limit,
        [FromQuery] int skip = 0)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var transfers = await unitOfWork.TransferRepository.QueryTransfers(new TransferFilter
        {
            Owner = subject,
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
            Skip = skip,
            Limit = limit ?? int.MaxValue,
        });

        return transfers.ToResultList(t => new Transfer()
        {
            FederatedStreamId = new FederatedStreamId
            {
                Registry = t.RegistryName,
                StreamId = t.CertificateId
            },
            ReceiverId = t.ReceiverId.ToString(),
            Start = t.StartDate.ToUnixTimeSeconds(),
            End = t.EndDate.ToUnixTimeSeconds(),
            Quantity = t.Quantity,
            GridArea = t.GridArea,
        });
    }

    /// <summary>
    /// Returns a list of aggregates transfers, for all certificates transferred to another wallet for the authenticated user based.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="timeAggregate">The size of each bucket in the aggregation</param>
    /// <param name="timeZone">The time zone. See https://en.wikipedia.org/wiki/List_of_tz_database_time_zones for a list of valid time zones.</param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <param name="skip">The number of items to skip.</param>
    /// <param name="limit">The number of items to return.</param>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="400">If the time zone is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/aggregate-transfers")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedTransfers>>> AggregateTransfers(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] TimeAggregate timeAggregate,
        [FromQuery] string timeZone,
        [FromQuery] long? start,
        [FromQuery] long? end,
        [FromQuery] int? limit,
        [FromQuery] int skip = 0)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();
        if (!timeZone.TryParseTimeZone(out var timeZoneInfo)) return BadRequest("Invalid time zone");

        var transfers = await unitOfWork.TransferRepository.QueryAggregatedTransfers(new TransferFilter
        {
            Owner = subject,
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
            Skip = skip,
            Limit = limit ?? int.MaxValue,
        }, (Models.TimeAggregate)timeAggregate, timeZone);

        return transfers.ToResultList(t => new AggregatedTransfers()
        {
            Start = t.Start.ToUnixTimeSeconds(),
            End = t.End.ToUnixTimeSeconds(),
            Quantity = t.Quantity,
        });
    }

    /// <summary>
    /// Queues a request to transfer a certificate to another wallet for the authenticated user.
    /// </summary>
    /// <param name="bus"></param>
    /// <param name="request"></param>
    /// <response code="202">Transfer request has been queued for processing.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost]
    [Route("v1/transfers")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TransferResponse>> TransferCertificate(
        [FromServices] IBus bus,
        [FromBody] TransferRequest request
    )
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var command = new TransferCertificateCommand
        {
            TransferRequestId = Guid.NewGuid(),
            Owner = subject,
            Registry = request.CertificateId.Registry,
            CertificateId = request.CertificateId.StreamId,
            Quantity = request.Quantity,
            Receiver = request.ReceiverId,
            HashedAttributes = request.HashedAttributes,
        };

        await bus.Publish(command);

        return Accepted(new TransferResponse()
        {
            TransferRequestId = command.TransferRequestId,
        });
    }
}

#region Records

/// <summary>
/// A transfer record of a transfer of a part of a certificate to another wallet.
/// </summary>
public record Transfer()
{
    public required FederatedStreamId FederatedStreamId { get; init; }
    public required string ReceiverId { get; init; }
    public required long Quantity { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
}

/// <summary>
/// A request to transfer a certificate to another wallet.
/// </summary>
public record TransferRequest()
{
    /// <summary>
    /// The federated stream id of the certificate to transfer.
    /// </summary>
    public required FederatedStreamId CertificateId { get; init; }

    /// <summary>
    /// The id of the wallet to transfer the certificate to.
    /// </summary>
    public required Guid ReceiverId { get; init; }

    /// <summary>
    /// The quantity of the certificate to transfer.
    /// </summary>
    public required uint Quantity { get; init; }

    /// <summary>
    /// List of hashed attributes to transfer with the certificate.
    /// </summary>
    public required string[] HashedAttributes { get; init; }
}

/// <summary>
/// A response to a transfer request.
/// </summary>
public record TransferResponse()
{
    /// <summary>
    /// The id of the transfer request.
    /// </summary>
    public required Guid TransferRequestId { get; init; }
}

/// <summary>
/// A result of aggregated transfers.
/// </summary>
public record AggregatedTransfers()
{
    /// <summary>
    /// The start of the aggregated period.
    /// </summary>
    public required long Start { get; init; }

    /// <summary>
    /// The end of the aggregated period.
    /// </summary>
    public required long End { get; init; }

    /// <summary>
    /// The quantity of the aggregated transfers.
    /// </summary>
    public required long Quantity { get; init; }
}

#endregion
