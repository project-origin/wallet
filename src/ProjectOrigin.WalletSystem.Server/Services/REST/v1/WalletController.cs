using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[Authorize]
[ApiController]
public class WalletController : ControllerBase
{
    /// <summary>
    /// Gets all certificates in the wallet that are available for use.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/certificates")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<GranularCertificate>>> GetCertificates([FromServices] IUnitOfWork unitOfWork, [FromQuery] long? start, [FromQuery] long? end)
    {
        var subject = User.GetSubject();

        var certificates = await unitOfWork.CertificateRepository.GetAllOwnedCertificates(subject, new CertificatesFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null
        });

        return new ResultList<GranularCertificate> { Result = certificates.Select(c => c.MapToV1()) };
    }

    /// <summary>
    /// Gets all claims in the wallet
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/claims")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<Claim>>> GetClaims([FromServices] IUnitOfWork unitOfWork, [FromQuery] long? start, [FromQuery] long? end)
    {
        var owner = User.GetSubject();

        var claims = await unitOfWork.CertificateRepository.GetClaims(owner, new ClaimFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
        });

        return new ResultList<Claim> { Result = claims.Select(c => c.MapToV1()) };
    }

    /// <summary>
    /// Aggregates certificates based on the specified time zone and time range.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="timeAggregate">The size of each bucket in the aggregation</param>
    /// <param name="timeZone">The time zone. See https://en.wikipedia.org/wiki/List_of_tz_database_time_zones for a list of valid time zones.</param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <param name="type">Filter the type of certificates to return.</param>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/aggregate-certificates")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedCertificates>>> AggregateCertificates(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] TimeAggregate timeAggregate,
        [FromQuery] string timeZone,
        [FromQuery] long? start,
        [FromQuery] long? end,
        [FromQuery] CertificateType? type)
    {
        var owner = User.GetSubject();
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZone);

        var certificates = await unitOfWork.CertificateRepository.GetAllOwnedCertificates(owner, new CertificatesFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
            Type = type != null ? (GranularCertificateType)type.Value : null
        });

        return new ResultList<AggregatedCertificates>
        {
            Result = certificates
                .GroupBy(cert => cert.CertificateType)
                .SelectMany(typeGroup => typeGroup
                    .GroupByTime(x => x.StartDate, (Models.TimeAggregate)timeAggregate, timeZoneInfo)
                    .Select(timeGroup => new AggregatedCertificates
                    {
                        Type = (CertificateType)typeGroup.Key,
                        Quantity = timeGroup.Sum(certificate => certificate.Slices.Sum(slice => slice.Quantity)),
                        Start = timeGroup.Min(certificate => certificate.StartDate).ToUnixTimeSeconds(),
                        End = timeGroup.Max(certificate => certificate.EndDate).ToUnixTimeSeconds(),
                    }))
        };
    }

    /// <summary>
    /// Aggregates claims based on the specified time zone and time range.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="timeAggregate">The size of each bucket in the aggregation</param>
    /// <param name="timeZone">The time zone. See https://en.wikipedia.org/wiki/List_of_tz_database_time_zones for a list of valid time zones.</param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/aggregate-claims")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedClaims>>> AggregateClaims(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] TimeAggregate timeAggregate,
        [FromQuery] string timeZone,
        [FromQuery] long? start,
        [FromQuery] long? end)
    {
        var owner = User.GetSubject();
        var timeZoneInfo = TimeZoneInfo.FindSystemTimeZoneById(timeZone);

        var claims = await unitOfWork.CertificateRepository.GetClaims(owner, new ClaimFilter
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
}
