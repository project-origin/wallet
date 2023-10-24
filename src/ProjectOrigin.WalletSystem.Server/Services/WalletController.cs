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
                    Registry = c.Registry,
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
