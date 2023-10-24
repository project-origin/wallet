using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Services;

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
                CertificateType = c.CertificateType,
                Attributes = new ApiAttributes
                {
                    AssetId = c.Attributes.FirstOrDefault(a => a.Key.Equals("AssetId", StringComparison.InvariantCultureIgnoreCase))?.Value,
                    FuelCode = c.Attributes.FirstOrDefault(a => a.Key.Equals("FuelCode", StringComparison.InvariantCultureIgnoreCase))?.Value,
                    TechCode = c.Attributes.FirstOrDefault(a => a.Key.Equals("TechCode", StringComparison.InvariantCultureIgnoreCase))?.Value
                }
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
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null ,
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
                Attributes = new ApiAttributes
                {
                    AssetId = c.ProductionAttributes.FirstOrDefault(a => a.Key.Equals("AssetId", StringComparison.InvariantCultureIgnoreCase))?.Value,
                    FuelCode = c.ProductionAttributes.FirstOrDefault(a => a.Key.Equals("FuelCode", StringComparison.InvariantCultureIgnoreCase))?.Value,
                    TechCode = c.ProductionAttributes.FirstOrDefault(a => a.Key.Equals("TechCode", StringComparison.InvariantCultureIgnoreCase))?.Value
                }
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
                Attributes = new ApiAttributes
                {
                    AssetId = c.ConsumptionAttributes.FirstOrDefault(a => a.Key.Equals("AssetId", StringComparison.InvariantCultureIgnoreCase))?.Value,
                    FuelCode = c.ConsumptionAttributes.FirstOrDefault(a => a.Key.Equals("FuelCode", StringComparison.InvariantCultureIgnoreCase))?.Value,
                    TechCode = c.ConsumptionAttributes.FirstOrDefault(a => a.Key.Equals("TechCode", StringComparison.InvariantCultureIgnoreCase))?.Value
                }
            }
        }).ToArray();

        return new ResultModel<ApiClaim> { Result = mapped};
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
    public required GranularCertificateType CertificateType { get; init; } //TODO: This type or custom?
    public required ApiAttributes Attributes { get; init; }
}

public record ApiClaim
{
    public required Guid ClaimId { get; init; }
    public required uint Quantity { get; init; }
    public required ApiClaimCertificateInfo ProductionCertificate { get; init; }
    public required ApiClaimCertificateInfo ConsumptionCertificate { get; init; }
}

public record ApiClaimCertificateInfo
{
    public required ApiFederatedStreamId FederatedStreamId { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
    public required ApiAttributes Attributes { get; init; }
}
