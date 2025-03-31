using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Web.Resource;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.Services.REST.v1;

[Authorize]
[ApiController]
public class ClaimsController : ControllerBase
{
    private readonly IClaimMetrics _claimMetrics;

    public ClaimsController(IClaimMetrics claimMetrics)
    {
        _claimMetrics = claimMetrics;
    }
    /// <summary>
    /// Gets all claims in the wallet
    /// </summary>
    /// <response code="200">Returns all the indiviual claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/claims")]
    [RequiredScope("po:claims:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<Claim, PageInfo>>> GetClaims(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] GetClaimsQueryParameters param)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var claims = await unitOfWork.ClaimRepository.QueryClaims(new QueryClaimsFilter
        {
            Owner = subject,
            Start = param.Start != null ? DateTimeOffset.FromUnixTimeSeconds(param.Start.Value) : null,
            End = param.End != null ? DateTimeOffset.FromUnixTimeSeconds(param.End.Value) : null,
            Skip = param.Skip,
            Limit = param.Limit ?? int.MaxValue,
        });

        return claims.ToResultList(c => c.MapToV1());
    }

    /// <summary>
    /// Gets all claims in the wallet
    /// </summary>
    /// <response code="200">Returns all the indiviual claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/claims/cursor")]
    [RequiredScope("po:claims:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<Claim, PageInfoCursor>>> GetClaimsCursor(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] GetClaimsQueryParametersCursor param)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var claims = await unitOfWork.ClaimRepository.QueryClaimsCursor(new QueryClaimsFilterCursor
        {
            Owner = subject,
            Start = param.Start != null ? DateTimeOffset.FromUnixTimeSeconds(param.Start.Value) : null,
            End = param.End != null ? DateTimeOffset.FromUnixTimeSeconds(param.End.Value) : null,
            UpdatedSince = param.UpdatedSince != null ? DateTimeOffset.FromUnixTimeSeconds(param.UpdatedSince.Value) : null,
            Limit = param.Limit ?? int.MaxValue,
        });

        return claims.ToResultList(c => c.MapToV1());
    }

    /// <summary>
    /// Returns a list of aggregates claims for the authenticated user based on the specified time zone and time range.
    /// </summary>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="400">If the time zone is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/aggregate-claims")]
    [RequiredScope("po:claims:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedClaims, PageInfo>>> AggregateClaims(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] AggregateClaimsQueryParameters param)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();
        if (!param.TimeZone.TryParseTimeZone(out var timeZoneInfo)) return BadRequest("Invalid time zone");

        var result = await unitOfWork.ClaimRepository.QueryAggregatedClaims(new QueryAggregatedClaimsFilter
        {
            Owner = subject,
            Start = param.Start != null ? DateTimeOffset.FromUnixTimeSeconds(param.Start.Value) : null,
            End = param.End != null ? DateTimeOffset.FromUnixTimeSeconds(param.End.Value) : null,
            Skip = param.Skip,
            Limit = param.Limit ?? int.MaxValue,
            TimeAggregate = (Models.TimeAggregate)param.TimeAggregate,
            TimeZone = param.TimeZone
        });

        return result.ToResultList(c => new AggregatedClaims()
        {
            Start = c.Start.ToUnixTimeSeconds(),
            End = c.End.ToUnixTimeSeconds(),
            Quantity = c.Quantity,
        });
    }

    /// <summary>
    /// Queues a request to claim two certificate for a given quantity.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="serviceOptions"></param>
    /// <param name="request">The claim request</param>
    /// <response code="202">Claim request has been queued for processing.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost]
    [Route("v1/claims")]
    [RequiredScope("po:claims:create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ClaimResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ClaimResponse>> ClaimCertificate(
        [FromServices] IUnitOfWork unitOfWork,
        [FromServices] IOptions<ServiceOptions> serviceOptions,
        [FromBody] ClaimRequest request
    )
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var prodCert = await unitOfWork.CertificateRepository.GetCertificate(request.ProductionCertificateId.Registry, request.ProductionCertificateId.StreamId);
        var conCert = await unitOfWork.CertificateRepository.GetCertificate(request.ConsumptionCertificateId.Registry, request.ConsumptionCertificateId.StreamId);

        if (prodCert == null)
        {
            return BadRequest($"Unknown production certificate. Registry {request.ProductionCertificateId.Registry} and id {request.ProductionCertificateId.StreamId}.");
        }
        if (conCert == null)
        {
            return BadRequest($"Unknown consumption certificate. Registry {request.ConsumptionCertificateId.Registry} and id {request.ConsumptionCertificateId.StreamId}.");
        }

        var prodWillBeAvailable = await unitOfWork.CertificateRepository.GetRegisteringAndAvailableQuantity(request.ProductionCertificateId.Registry, request.ProductionCertificateId.StreamId, subject);
        var conWillBeAvailable = await unitOfWork.CertificateRepository.GetRegisteringAndAvailableQuantity(request.ConsumptionCertificateId.Registry, request.ConsumptionCertificateId.StreamId, subject);

        if (prodWillBeAvailable < request.Quantity)
        {
            return BadRequest($"Claim is not allowed. Production certificate does not have enough quantity to claim requested amount. Production certificate amount: {prodWillBeAvailable}. Requested claim quantity: {request.Quantity}");
        }
        if (conWillBeAvailable < request.Quantity)
        {
            return BadRequest($"Claim is not allowed. Consumption certificate does not have enough quantity to claim requested amount. Consumption certificate amount: {conWillBeAvailable}. Requested claim quantity: {request.Quantity}");
        }

        var command = new ClaimCertificateCommand
        {
            Owner = subject,
            ClaimId = Guid.NewGuid(),
            ConsumptionRegistry = request.ConsumptionCertificateId.Registry,
            ConsumptionCertificateId = request.ConsumptionCertificateId.StreamId,
            ProductionRegistry = request.ProductionCertificateId.Registry,
            ProductionCertificateId = request.ProductionCertificateId.StreamId,
            Quantity = request.Quantity,
        };

        await unitOfWork.RequestStatusRepository.InsertRequestStatus(new Models.RequestStatus
        {
            RequestId = command.ClaimId,
            Owner = subject,
            Type = RequestStatusType.Claim,
            Created = DateTimeOffset.UtcNow,
            Status = RequestStatusState.Pending
        });

        await unitOfWork.OutboxMessageRepository.Create(new OutboxMessage
        {
            Created = DateTimeOffset.UtcNow.ToUtcTime(),
            Id = Guid.NewGuid(),
            MessageType = typeof(ClaimCertificateCommand).ToString(),
            JsonPayload = JsonSerializer.Serialize(command)
        });

        unitOfWork.Commit();
        _claimMetrics.IncrementClaimIntents();

        return Accepted(serviceOptions.Value.PathBase + "/v1/request-status/" + command.ClaimId, new ClaimResponse()
        {
            ClaimRequestId = command.ClaimId,
        });
    }
}

#region Records

public record GetClaimsQueryParametersCursor
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
    /// Filter the type of certificates to return.
    /// </summary>
    public CertificateType? Type { get; init; }

    /// <summary>
    /// The number of items to return.
    /// </summary>
    public int? Limit { get; init; }

    /// <summary>
    /// The time of the last update in Unix time in seconds.
    /// </summary>
    public long? UpdatedSince { get; init; }
}


public record GetClaimsQueryParameters
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

public record AggregateClaimsQueryParameters
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
/// A claim record representing a claim of a production and consumption certificate.
/// </summary>
public record Claim()
{
    public required Guid ClaimId { get; init; }
    public required uint Quantity { get; init; }
    public required ClaimedCertificate ProductionCertificate { get; init; }
    public required ClaimedCertificate ConsumptionCertificate { get; init; }
    public required long UpdatedAt { get; init; }
}


/// <summary>
/// Info record of a claimed certificate.
/// </summary>
public record ClaimedCertificate()
{
    /// <summary>
    /// The id of the claimed certificate.
    /// </summary>
    public required FederatedStreamId FederatedStreamId { get; init; }

    /// <summary>
    /// The start period of the claimed certificate.
    /// </summary>
    public required long Start { get; init; }

    /// <summary>
    /// The end period the claimed certificate.
    /// </summary>
    public required long End { get; init; }

    /// <summary>
    /// The Grid Area of the claimed certificate.
    /// </summary>
    public required string GridArea { get; init; }

    /// <summary>
    /// The attributes of the claimed certificate.
    /// </summary>
    public required Dictionary<string, string> Attributes { get; init; }
}

/// <summary>
/// A request to claim a production and consumption certificate.
/// </summary>
public record ClaimRequest()
{
    /// <summary>
    /// The id of the production certificate to claim.
    /// </summary>
    public required FederatedStreamId ProductionCertificateId { get; init; }

    /// <summary>
    /// The id of the consumption certificate to claim.
    /// </summary>
    public required FederatedStreamId ConsumptionCertificateId { get; init; }

    /// <summary>
    /// The quantity of the certificates to claim.
    /// </summary>
    public required uint Quantity { get; init; }
}

/// <summary>
/// A response to a claim request.
/// </summary>
public record ClaimResponse()
{
    /// <summary>
    /// The id of the claim request.
    /// </summary>
    public required Guid ClaimRequestId { get; init; }
}

/// <summary>
/// A result of aggregated claims.
/// </summary>
public record AggregatedClaims()
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
    /// The quantity of the aggregated claims.
    /// </summary>
    public required long Quantity { get; init; }
}

#endregion
