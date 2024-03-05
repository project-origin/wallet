using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[Authorize]
[ApiController]
public class ClaimsController : ControllerBase
{
    /// <summary>
    /// Gets all claims in the wallet
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <param name="skip">The number of items to skip.</param>
    /// <param name="limit">The number of items to return.</param>
    /// <response code="200">Returns all the indiviual claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/claims")]
    [RequiredScope("claim:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<Claim>>> GetClaims(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] long? start,
        [FromQuery] long? end,
        [FromQuery] int? limit,
        [FromQuery] int skip = 0)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var claims = await unitOfWork.ClaimRepository.QueryClaims(new ClaimFilter
        {
            Owner = subject,
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
            Skip = skip,
            Limit = limit ?? int.MaxValue,
        });

        return claims.ToResultList(c => c.MapToV1());
    }

    /// <summary>
    /// Returns a list of aggregates claims for the authenticated user based on the specified time zone and time range.
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
    [Route("v1/aggregate-claims")]
    [RequiredScope("claim:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedClaims>>> AggregateClaims(
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

        var result = await unitOfWork.ClaimRepository.QueryAggregatedClaims(new ClaimFilter
        {
            Owner = subject,
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
            Skip = skip,
            Limit = limit ?? int.MaxValue
        }, (Models.TimeAggregate)timeAggregate, timeZone);

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
    /// <param name="bus">The masstransit bus to queue the request to</param>
    /// <param name="request">The claim request</param>
    /// <response code="202">Claim request has been queued for processing.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpPost]
    [Route("v1/claims")]
    [RequiredScope("claim:create")]
    [Produces("application/json")]
    [ProducesResponseType(typeof(ClaimResponse), StatusCodes.Status202Accepted)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ClaimResponse>> ClaimCertificate(
        [FromServices] IBus bus,
        [FromBody] ClaimRequest request
    )
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

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

        await bus.Publish(command);

        return Accepted(new ClaimResponse()
        {
            ClaimRequestId = command.ClaimId,
        });
    }
}

#region Records

/// <summary>
/// A claim record representing a claim of a production and consumption certificate.
/// </summary>
public record Claim()
{
    public required Guid ClaimId { get; init; }
    public required uint Quantity { get; init; }
    public required ClaimedCertificate ProductionCertificate { get; init; }
    public required ClaimedCertificate ConsumptionCertificate { get; init; }
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
