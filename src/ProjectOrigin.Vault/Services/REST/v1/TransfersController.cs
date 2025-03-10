using System;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Services.REST.v1;

[Authorize]
[ApiController]
public class TransfersController : ControllerBase
{
    private readonly ITransferMetrics _transferMetrics;

    public TransfersController(ITransferMetrics transferMetrics)
    {
        _transferMetrics = transferMetrics;
    }

    /// <summary>
    /// Gets detailed list of all of the transfers that have been made to other wallets.
    /// </summary>
    /// <response code="200">Returns the individual transferes within the filter.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/transfers/cursor")]
    [RequiredScope("po:transfers:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<Transfer, PageInfoCursor>>> GetTransfersCursor(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] GetTransfersQueryParametersCursor param)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var transfers = await unitOfWork.TransferRepository.QueryTransfers(new QueryTransfersFilterCursor()
        {
            Owner = subject,
            Start = param.Start != null ? DateTimeOffset.FromUnixTimeSeconds(param.Start.Value) : null,
            End = param.End != null ? DateTimeOffset.FromUnixTimeSeconds(param.End.Value) : null,
            UpdatedSince = param.UpdatedSince != null ? DateTimeOffset.FromUnixTimeSeconds(param.UpdatedSince.Value) : null,
            Limit = param.Limit ?? int.MaxValue,
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
    /// Gets detailed list of all of the transfers that have been made to other wallets.
    /// </summary>
    /// <response code="200">Returns the individual transferes within the filter.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/transfers")]
    [RequiredScope("po:transfers:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<Transfer, PageInfo>>> GetTransfers(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] GetTransfersQueryParameters param)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var transfers = await unitOfWork.TransferRepository.QueryTransfers(new QueryTransfersFilter
        {
            Owner = subject,
            Start = param.Start != null ? DateTimeOffset.FromUnixTimeSeconds(param.Start.Value) : null,
            End = param.End != null ? DateTimeOffset.FromUnixTimeSeconds(param.End.Value) : null,
            Skip = param.Skip,
            Limit = param.Limit ?? int.MaxValue,
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
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="400">If the time zone is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/aggregate-transfers")]
    [RequiredScope("po:transfers:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedTransfers, PageInfo>>> AggregateTransfers(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] AggregateTransfersQueryParameters param)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();
        if (!param.TimeZone.TryParseTimeZone(out var timeZoneInfo)) return BadRequest("Invalid time zone");

        var transfers = await unitOfWork.TransferRepository.QueryAggregatedTransfers(new QueryAggregatedTransfersFilter
        {
            Owner = subject,
            Start = param.Start != null ? DateTimeOffset.FromUnixTimeSeconds(param.Start.Value) : null,
            End = param.End != null ? DateTimeOffset.FromUnixTimeSeconds(param.End.Value) : null,
            Skip = param.Skip,
            Limit = param.Limit ?? int.MaxValue,
            TimeAggregate = (Models.TimeAggregate)param.TimeAggregate,
            TimeZone = param.TimeZone
        });

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
    /// <param name="unitOfWork"></param>
    /// <param name="serviceOptions"></param>
    /// <param name="request"></param>
    /// <response code="202">Transfer request has been queued for processing.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost]
    [Route("v1/transfers")]
    [RequiredScope("po:transfers:create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(TransferResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<TransferResponse>> TransferCertificate(
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] IOptions<ServiceOptions> serviceOptions,
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

        await unitOfWork.RequestStatusRepository.InsertRequestStatus(new Models.RequestStatus
        {
            RequestId = command.TransferRequestId,
            Owner = subject,
            Type = RequestStatusType.Transfer,
            Created = DateTimeOffset.UtcNow,
            Status = RequestStatusState.Pending
        });

        await unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
        {
            Created = DateTimeOffset.UtcNow.ToUtcTime(),
            Id = Guid.NewGuid(),
            MessageType = typeof(TransferCertificateCommand).ToString(),
            JsonPayload = JsonSerializer.Serialize(command)
        });

        unitOfWork.Commit();

        _transferMetrics.IncrementTransferIntents();

        return Accepted(serviceOptions.Value.PathBase + "/v1/request-status/" + command.TransferRequestId, new TransferResponse()
        {
            TransferRequestId = command.TransferRequestId,
        });
    }
}

#region Records

public record GetTransfersQueryParametersCursor
{
    /// <summary>
    /// The start of the time range in Unix time in seconds.
    /// </summary>
    public long? Start { get; init; }

    /// <summary>
    /// The end of the time range in Unix time in seconds.
    /// </summary>
    public long? End { get; init; }

    /// <summary>
    /// The number of items to return.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// The time of the last update in Unix time in seconds.
    /// </summary>
    public long? UpdatedSince { get; init; }
}

public record GetTransfersQueryParameters
{
    /// <summary>
    /// The start of the time range in Unix time in seconds.
    /// </summary>
    public long? Start { get; init; }

    /// <summary>
    /// The end of the time range in Unix time in seconds.
    /// </summary>
    public long? End { get; init; }

    /// <summary>
    /// The number of items to return.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// The number of items to skip.
    /// </summary>
    [DefaultValue(0)]
    public int Skip { get; init; } = 0;
}

public record AggregateTransfersQueryParameters
{
    /// <summary>
    /// The size of each bucket in the aggregation
    /// </summary>
    public required TimeAggregate TimeAggregate { get; init; }

    /// <summary>
    /// The time zone. See https://en.wikipedia.org/wiki/List_of_tz_database_time_zones for a list of valid time zones.
    /// </summary>
    public required string TimeZone { get; init; }

    /// <summary>
    /// The start of the time range in Unix time in seconds.
    /// </summary>
    public long? Start { get; init; }

    /// <summary>
    /// The end of the time range in Unix time in seconds.
    /// </summary>
    public long? End { get; init; }

    /// <summary>
    /// The number of items to return.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// The number of items to skip.
    /// </summary>
    [DefaultValue(0)]
    public int Skip { get; init; } = 0;
}

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
