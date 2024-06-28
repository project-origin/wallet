using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web.Resource;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using System.Threading.Tasks;
using System;

namespace ProjectOrigin.WalletSystem.Server.Services.REST.v1;

[Authorize]
[ApiController]
public class RequestStatusController : ControllerBase
{
    /// <summary>
    /// Gets status of specific request.
    /// </summary>
    /// <param name="unitOfWork"></param>
    /// <param name="requestId">The ID of the request.</param>
    /// <response code="200">The request status was found.</response>
    /// <response code="401">If the user is not authenticated.</response>
    /// <response code="404">If the request specified is not found for the user.</response>
    [HttpGet]
    [Route("v1/request-status/{requestId}")]
    [RequiredScope("po:requestStatus:read")]
    [ProducesResponseType(typeof(RequestStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(void), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(void), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RequestStatusResponse>> GetRequestStatus(
        [FromServices] IUnitOfWork unitOfWork,
        [FromRoute] Guid requestId)
    {
        if (!User.TryGetSubject(out var subject)) return Unauthorized();

        var requestStatus = await unitOfWork.RequestStatusRepository.GetRequestStatus(requestId, subject);

        if (requestStatus == null) return NotFound();

        return Ok(new RequestStatusResponse { Status = requestStatus.Status.MapToV1() });
    }
}

#region Records

/// <summary>
/// Request status response.
/// </summary>
public record RequestStatusResponse()
{
    /// <summary>
    /// The status of the request.
    /// </summary>
    public required RequestStatus Status { get; init; }
}

#endregion
