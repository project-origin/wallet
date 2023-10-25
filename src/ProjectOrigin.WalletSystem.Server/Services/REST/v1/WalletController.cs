using System;
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

        return new ResultList<GranularCertificate> { Result = certificates.Select(c => c.ToV1()).ToArray() };
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

        return new ResultList<Claim> { Result = claims.Select(c => c.ToV1()).ToArray() };
    }
}
