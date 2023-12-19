using System;
using System.Linq;
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
public class ClaimsController : ControllerBase
{
    /// <summary>
    /// Gets all claims in the wallet
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <response code="200">Returns all the indiviual claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/claims")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<Claim>>> GetClaims([FromServices] IUnitOfWork unitOfWork, [FromQuery] long? start, [FromQuery] long? end)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var claims = await unitOfWork.CertificateRepository.GetClaims(subject, new ClaimFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
        });

        return new ResultList<Claim> { Result = claims.Select(c => c.MapToV1()) };
    }

    /// <summary>
    /// Returns a list of aggregates claims for the authenticated user based on the specified time zone and time range.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="timeAggregate">The size of each bucket in the aggregation</param>
    /// <param name="timeZone">The time zone. See https://en.wikipedia.org/wiki/List_of_tz_database_time_zones for a list of valid time zones.</param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="400">If the time zone is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/aggregate-claims")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedClaims>>> AggregateClaims(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] TimeAggregate timeAggregate,
        [FromQuery] string timeZone,
        [FromQuery] long? start,
        [FromQuery] long? end)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();
        if (!timeZone.TryParseTimeZone(out var timeZoneInfo)) return BadRequest("Invalid time zone");

        var claims = await unitOfWork.CertificateRepository.GetClaims(subject, new ClaimFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
        });

        return new ResultList<AggregatedClaims>
        {
            Result = claims
                .GroupByTime(x => x.ProductionStart, (Models.TimeAggregate)timeAggregate, timeZoneInfo)
                .Select(group => new AggregatedClaims
                {
                    Quantity = group.Sum(claim => claim.Quantity),
                    Start = group.Min(claim => claim.ProductionStart).ToUnixTimeSeconds(),
                    End = group.Max(claim => claim.ProductionEnd).ToUnixTimeSeconds(),
                })
        };
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
