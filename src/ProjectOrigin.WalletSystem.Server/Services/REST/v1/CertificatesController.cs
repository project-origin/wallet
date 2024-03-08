using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
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
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/certificates")]
    [RequiredScope("po:certificates:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<GranularCertificate>>> GetCertificates(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] GetCertificatesQueryParameters param)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var certificates = await unitOfWork.CertificateRepository.QueryAvailableCertificates(new QueryCertificatesFilter
        {
            Owner = subject,
            Start = param.Start != null ? DateTimeOffset.FromUnixTimeSeconds(param.Start.Value) : null,
            End = param.End != null ? DateTimeOffset.FromUnixTimeSeconds(param.End.Value) : null,
            Type = param.Type != null ? (GranularCertificateType)param.Type.Value : null,
            Skip = param.Skip,
            Limit = param.Limit ?? int.MaxValue,
        });

        return certificates.ToResultList(c => c.MapToV1());
    }

    /// <summary>
    /// Returns aggregates certificates that are <b>available</b> to use, based on the specified time zone and time range.
    /// </summary>
    /// <response code="200">Returns the aggregated claims.</response>
    /// <response code="400">If the time zone is invalid.</response>
    /// <response code="401">If the user is not authenticated.</response>
    [HttpGet]
    [Route("v1/aggregate-certificates")]
    [RequiredScope("po:certificates:read")]
    [Produces("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<ResultList<AggregatedCertificates>>> AggregateCertificates(
        [FromServices] IUnitOfWork unitOfWork,
        [FromQuery] AggregateCertificatesQueryParameters param)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();
        if (!param.TimeZone.TryParseTimeZone(out var timeZoneInfo)) return BadRequest("Invalid time zone");

        var certificates = await unitOfWork.CertificateRepository.QueryAggregatedAvailableCertificates(new QueryAggregatedCertificatesFilter
        {
            Owner = subject,
            Start = param.Start != null ? DateTimeOffset.FromUnixTimeSeconds(param.Start.Value) : null,
            End = param.End != null ? DateTimeOffset.FromUnixTimeSeconds(param.End.Value) : null,
            Type = param.Type != null ? (GranularCertificateType)param.Type.Value : null,
            Skip = param.Skip,
            Limit = param.Limit ?? int.MaxValue,
            TimeAggregate = (Models.TimeAggregate)param.TimeAggregate,
            TimeZone = param.TimeZone
        });

        return certificates.ToResultList(c => new AggregatedCertificates
        {
            Start = c.Start.ToUnixTimeSeconds(),
            End = c.End.ToUnixTimeSeconds(),
            Quantity = c.Quantity,
            Type = (CertificateType)c.Type
        });
    }
}

#region Records
public record GetCertificatesQueryParameters
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
    /// The number of items to skip.
    /// </summary>
    [DefaultValue(0)]
    public int Skip { get; init; }
}

public record AggregateCertificatesQueryParameters
{
    /// <summary>
    /// The size of each bucket in the aggregation.
    /// </summary>
    public required TimeAggregate TimeAggregate { get; init; }

    /// <summary>
    /// The time zone. See https://en.wikipedia.org/wiki/List_of_tz_database_time_zones for a list of valid time zones.
    /// </summary>
    public required string TimeZone { get; init; }

    /// <summary>
    ///The start of the time range in Unix time in seconds.
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
    /// The number of items to skip.
    /// </summary>
    [DefaultValue(0)]
    public int Skip { get; init; }
}

/// <summary>
/// A certificate that is available to use in the wallet.
/// </summary>
public record GranularCertificate()
{
    /// <summary>
    /// The id of the certificate.
    /// </summary>
    public required FederatedStreamId FederatedStreamId { get; init; }

    /// <summary>
    /// The quantity available on the certificate.
    /// </summary>
    public required uint Quantity { get; init; }

    /// <summary>
    /// The start of the certificate.
    /// </summary>
    public required long Start { get; init; }

    /// <summary>
    /// The end of the certificate.
    /// </summary>
    public required long End { get; init; }

    /// <summary>
    /// The Grid Area of the certificate.
    /// </summary>
    public required string GridArea { get; init; }

    /// <summary>
    /// The type of certificate (production or consumption).
    /// </summary>
    public required CertificateType CertificateType { get; init; }

    /// <summary>
    /// The attributes of the certificate.
    /// </summary>
    public required Dictionary<string, string> Attributes { get; init; }
}


/// <summary>
/// A result of aggregated certificates that is available to use in the wallet.
/// </summary>
public record AggregatedCertificates()
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
    /// The quantity of the aggregated certificates.
    /// </summary>
    public required long Quantity { get; init; }

    /// <summary>
    /// The type of the aggregated certificates.
    /// </summary>
    public required CertificateType Type { get; init; }
}

#endregion
