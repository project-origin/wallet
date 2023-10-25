using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using Swashbuckle.AspNetCore.Annotations;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

/*
 * - add v1 to route
- attributes to dictionary (gerne med eksempler i open api spec)
- Læg det i et v1 namepspace og dupliker GranularCertificateType-enum
- Fjern "Api"-prefix fra components-klasser
- Under Services hav en "Grpc"-mappe og en "Rest"-mappe
 *
 */

[Authorize]
[ApiController]
public class WalletController : ControllerBase
{
    [HttpGet]
    [Route("api/certificates")]
    [Produces("application/json")]
    public async Task<ActionResult<ResultList<GranularCertificate>>> GetCertificates([FromServices] IUnitOfWork unitOfWork)
    {
        var subject = User.GetSubject();

        var certificates = await unitOfWork.CertificateRepository.GetAllOwnedCertificates(subject);

        var mapped = certificates.Select(c => new GranularCertificate
        {
            FederatedStreamId = new FederatedStreamId
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

        return new ResultList<GranularCertificate> { Result = mapped };
    }

    [HttpGet]
    [Route("api/claims")]
    [Produces("application/json")]
    public async Task<ActionResult<ResultList<Claim>>> GetClaims([FromServices] IUnitOfWork unitOfWork, [FromQuery] long? start, [FromQuery] long? end)
    {
        var owner = User.GetSubject();

        var claims = await unitOfWork.CertificateRepository.GetClaims(owner, new ClaimFilter
        {
            Start = start != null ? DateTimeOffset.FromUnixTimeSeconds(start.Value) : null,
            End = end != null ? DateTimeOffset.FromUnixTimeSeconds(end.Value) : null,
        });

        var mapped = claims.Select(c => new Claim
        {
            ClaimId = c.Id,
            Quantity = c.Quantity,
            ProductionCertificate = new ClaimedCertificate
            {
                FederatedStreamId = new FederatedStreamId
                {
                    Registry = c.ProductionRegistryName,
                    StreamId = c.ProductionCertificateId
                },
                Start = c.ProductionStart.ToUnixTimeSeconds(),
                End = c.ProductionEnd.ToUnixTimeSeconds(),
                GridArea = c.ProductionGridArea,
                Attributes = c.ProductionAttributes.OrderBy(a => a.Key).ToDictionary(a => a.Key, a => a.Value) //TODO: Key to be camelcase??? 
            },
            ConsumptionCertificate = new ClaimedCertificate
            {
                FederatedStreamId = new FederatedStreamId
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

        return new ResultList<Claim> { Result = mapped };
    }
}

public record ResultList<T>
{
    public required T[] Result { get; init; }
}

public record FederatedStreamId
{
    public required string Registry { get; init; }
    public required Guid StreamId { get; init; }
}

public record GranularCertificate
{
    public required FederatedStreamId FederatedStreamId { get; init; }
    public required uint Quantity { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
    public required CertificateType CertificateType { get; init; }
    /// <summary>
    /// Bla
    /// </summary>
    /// <example>foo</example>
    public required Dictionary<string, string> Attributes { get; init; }
}

public enum CertificateType
{
    Consumption = 1,
    Production = 2
}

public record ClaimedCertificate
{
    public required FederatedStreamId FederatedStreamId { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
    public required string GridArea { get; init; }
    public required Dictionary<string, string> Attributes { get; init; }
}

public record Claim
{
    public required Guid ClaimId { get; init; }
    public required uint Quantity { get; init; }
    public required ClaimedCertificate ProductionCertificate { get; init; }
    public required ClaimedCertificate ConsumptionCertificate { get; init; }
}
