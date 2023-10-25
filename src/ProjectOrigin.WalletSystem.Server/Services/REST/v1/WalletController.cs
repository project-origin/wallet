using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

/*
 * - add v1 to route
- attributes to dictionary (gerne med eksempler i open api spec)
- Læg det i et v1 namepspace og dupliker GranularCertificateType-enum
- Fjern "Api"-prefix fra components-klasser
- Under Services hav en "Grpc"-mappe og en "Rest"-mappe
- Nedarv ApiGranularCertificate hvor child har en quantity property

 *
 */

[Authorize]
[ApiController]
public class WalletController : ControllerBase
{
    [HttpGet]
    [Route("api/certificates")]
    [Produces("application/json")]
    public async Task<ActionResult<ResultModel<ApiGranularCertificate>>> GetCertificates([FromServices] IUnitOfWork unitOfWork)
    {
        var subject = User.GetSubject();

        var certificates = await unitOfWork.CertificateRepository.GetAllOwnedCertificates(subject);

        var mapped = certificates.Select(c => new ApiGranularCertificate
        {
            FederatedStreamId = new ApiFederatedStreamId
            {
                Registry = c.RegistryName,
                StreamId = c.Id
            },
            Quantity = (uint)c.Slices.Sum(x => x.Quantity),
            Start = c.StartDate.ToUnixTimeSeconds(),
            End = c.EndDate.ToUnixTimeSeconds(),
            GridArea = c.GridArea,
            CertificateType = c.CertificateType == GranularCertificateType.Consumption ? CertificateType.Consumption : CertificateType.Production, //TODO: Mapper function
            Attributes = c.Attributes.OrderBy(a => a.Key).ToDictionary(a => a.Key, a => a.Value) //TODO: Key to be camelcase???
        })
            .ToArray();

        return new ResultModel<ApiGranularCertificate> { Result = mapped };
    }

    [HttpGet]
    [Route("api/claims")]
    [Produces("application/json")]
    public async Task<ActionResult<ResultModel<ApiClaim>>> GetClaims([FromServices] IUnitOfWork unitOfWork, [FromQuery] long? start, [FromQuery] long? end)
    {
        var owner = User.GetSubject();

        var claims = await unitOfWork.CertificateRepository.GetClaims(owner, new ClaimFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
        });

        var mapped = claims.Select(c => new ApiClaim
        {
            ClaimId = c.Id,
            Quantity = c.Quantity,
            ProductionCertificate = new ApiClaimCertificateInfo
            {
                FederatedStreamId = new ApiFederatedStreamId
                {
                    Registry = c.ProductionRegistryName,
                    StreamId = c.ProductionCertificateId
                },
                Start = c.ProductionStart.ToUnixTimeSeconds(),
                End = c.ProductionEnd.ToUnixTimeSeconds(),
                GridArea = c.ProductionGridArea,
                Attributes = c.ProductionAttributes.OrderBy(a => a.Key).ToDictionary(a => a.Key, a => a.Value) //TODO: Key to be camelcase???
            },
            ConsumptionCertificate = new ApiClaimCertificateInfo
            {
                FederatedStreamId = new ApiFederatedStreamId
                {
                    Registry = c.ConsumptionRegistryName,
                    StreamId = c.ConsumptionCertificateId
                },
                Start = c.ConsumptionStart.ToUnixTimeSeconds(),
                End = c.ConsumptionEnd.ToUnixTimeSeconds(),
                GridArea = c.ConsumptionGridArea,
                Attributes = c.ConsumptionAttributes.OrderBy(a => a.Key).ToDictionary(a => a.Key, a => a.Value) //TODO: Key to be camelcase???
            }
        }).ToArray();

        return new ResultModel<ApiClaim> { Result = mapped };
    }
}

public record ResultModel<T>
{
    public required T[] Result { get; init; }
}

public record ApiFederatedStreamId
{
    public required string Registry { get; init; }
    public required Guid StreamId { get; init; }
}

public record ApiAttributes
{
    public required string? AssetId { get; init; }
    public required string? TechCode { get; init; }
    public required string? FuelCode { get; init; }
}

public record ApiGranularCertificate
{
    public required ApiFederatedStreamId FederatedStreamId { get; init; }
    public required uint Quantity { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
    public required CertificateType CertificateType { get; init; }
    public required Dictionary<string, string> Attributes { get; init; }
}

public enum CertificateType
{
    Consumption = 1,
    Production = 2
}

public record ApiClaimCertificateInfo
{
    public required ApiFederatedStreamId FederatedStreamId { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
    public required Dictionary<string, string> Attributes { get; init; }
}

public record ApiClaim
{
    public required Guid ClaimId { get; init; }
    public required uint Quantity { get; init; }
    public required ApiClaimCertificateInfo ProductionCertificate { get; init; }
    public required ApiClaimCertificateInfo ConsumptionCertificate { get; init; }
}
