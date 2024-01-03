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
public class CertificatesController : ControllerBase
{
    /// <summary>
    /// Gets all certificates in the wallet that are <b>available</b> for use.
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
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var certificates = await unitOfWork.CertificateRepository.GetAllOwnedCertificates(subject, new CertificatesFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null
        });

        return new ResultList<GranularCertificate> { Result = certificates.Select(c => c.MapToV1()) };
    }

    /// <summary>
    /// Returns aggregates certificates that are <b>available</b> to use, based on the specified time zone and time range.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="timeAggregate">The size of each bucket in the aggregation</param>
    /// <param name="timeZone">The time zone. See https://en.wikipedia.org/wiki/List_of_tz_database_time_zones for a list of valid time zones.</param>
    /// <param name="start">The start of the time range in Unix time in seconds.</param>
    /// <param name="end">The end of the time range in Unix time in seconds.</param>
    /// <param name="type">Filter the type of certificates to return.</param>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="400">If the time zone is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/aggregate-certificates")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedCertificates>>> AggregateCertificates(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] TimeAggregate timeAggregate,
        [FromQuery] string timeZone,
        [FromQuery] long? start,
        [FromQuery] long? end,
        [FromQuery] CertificateType? type)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();
        if (!timeZone.TryParseTimeZone(out var timeZoneInfo)) return BadRequest("Invalid time zone");

        var certificates = await unitOfWork.CertificateRepository.GetAllOwnedCertificates(subject, new CertificatesFilter
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
}
