using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;

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
                Quantity = (uint)c.Slices.Sum(x => x.Quantity),
                Start = c.StartDate.ToUnixTimeSeconds(),
                End = c.EndDate.ToUnixTimeSeconds()
            })
            .ToArray();

        return new ResultModel<ApiGranularCertificate> { Result = mapped };
    }
}

public record ResultModel<T>
{
    public required T[] Result { get; init; }
}

public record ApiGranularCertificate
{
    public required uint Quantity { get; init; }
    public required long Start { get; init; }
    public required long End { get; init; }
}
