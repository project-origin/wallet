using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Helpers;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[Authorize]
[ApiController]
public class WalletController : ControllerBase
{
    [HttpGet]
    [Route("v1/certificates")]
    [Produces("application/json")]
    public async Task<ActionResult<ResultList<GranularCertificate>>> GetCertificates([FromServices] IUnitOfWork unitOfWork, [FromQuery] long? start, [FromQuery] long? end)
    {
        var subject = User.GetSubject();

        var certificates = await unitOfWork.CertificateRepository.GetAllOwnedCertificates(subject, new CertificatesFilter(SliceState.Available)
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null
        });

        return new ResultList<GranularCertificate> { Result = certificates.Select(c => c.MapToV1()).ToArray() };
    }

    [HttpGet]
    [Route("v1/claims")]
    [Produces("application/json")]
    public async Task<ActionResult<ResultList<Claim>>> GetClaims([FromServices] IUnitOfWork unitOfWork, [FromQuery] long? start, [FromQuery] long? end)
    {
        var owner = User.GetSubject();

        var claims = await unitOfWork.CertificateRepository.GetClaims(owner, new ClaimFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
        });

        return new ResultList<Claim> { Result = claims.Select(c => c.MapToV1()).ToArray() };
    }

    [HttpGet]
    [Route("v1/aggregate-certificates")]
    [Produces("application/json")]
    public async Task<ActionResult<ResultList<AggregationResult>>> AggregateCertificates([FromServices] IUnitOfWork unitOfWork, [FromQuery] TimeAggregate timeAggregate, [FromQuery] SliceState state, [FromQuery] long? start, [FromQuery] long? end, [FromQuery] CertificateType? type)
    {
        var owner = User.GetSubject();

        var certificates = await unitOfWork.CertificateRepository.GetAllOwnedCertificates(owner, new CertificatesFilter(state)
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
            Type = type != null ? Map(type.Value) : null
        });

        if (timeAggregate == TimeAggregate.Day)
        {
            return new ResultList<AggregationResult> { Result = GroupCertificatesHelper.GroupByDay(certificates).ToArray() };
        }

        throw new NotImplementedException($"timeAggregate value {timeAggregate} has not been implemented.");
    }

    [HttpGet]
    [Route("v1/aggregate-claims")]
    [Produces("application/json")]
    public async Task<ActionResult<ResultList<AggregationResult>>> AggregateClaims([FromServices] IUnitOfWork unitOfWork, [FromQuery] TimeAggregate timeAggregate, [FromQuery] long? start, [FromQuery] long? end)
    {
        var owner = User.GetSubject();

        var claims = await unitOfWork.CertificateRepository.GetClaims(owner, new ClaimFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
        });

        if (timeAggregate == TimeAggregate.Day)
        {
            return new ResultList<AggregationResult> { Result = GroupClaimsHelper.GroupByDay(claims).ToArray() };
        }

        throw new NotImplementedException($"timeAggregate value {timeAggregate} has not been implemented.");
    }

    private static GranularCertificateType Map(CertificateType type)
    {
        return type switch
        {
            CertificateType.Production => GranularCertificateType.Production,
            CertificateType.Consumption => GranularCertificateType.Consumption,
            _ => throw new ArgumentException($"Unsupported certificate type {type}")
        };
    }
}
