using AutoFixture;
using Microsoft.AspNetCore.Mvc;
using ProjectOrigin.Vault.Tests.TestClassFixtures;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Services.REST.v1;
using System;
using System.Security.Claims;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ProjectOrigin.Vault.Tests.TestExtensions;
using Xunit;
using RequestStatus = ProjectOrigin.Vault.Services.REST.v1.RequestStatus;

namespace ProjectOrigin.Vault.Tests.REST;

public class RequestStatusControllerTests : IClassFixture<PostgresDatabaseFixture>
{
    private readonly Fixture _fixture;
    private readonly PostgresDatabaseFixture _dbFixture;
    private readonly IUnitOfWork _unitOfWork;

    public RequestStatusControllerTests(PostgresDatabaseFixture postgresDatabaseFixture)
    {
        _fixture = new Fixture();
        _dbFixture = postgresDatabaseFixture;
        _unitOfWork = _dbFixture.CreateUnitOfWork();
    }

    [Fact]
    public async Task GetRequestStatus_NoContext_Unauthorized()
    {
        var controller = new RequestStatusController();

        var result = await controller.GetRequestStatus(
            _unitOfWork,
            Guid.NewGuid());

        result.Result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task GetRequestStatus_NotFound()
    {
        var subject = _fixture.Create<string>();
        var controller = new RequestStatusController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var result = await controller.GetRequestStatus(
            _unitOfWork,
            Guid.NewGuid());

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetRequestStatus()
    {
        var subject = _fixture.Create<string>();
        var controller = new RequestStatusController
        {
            ControllerContext = CreateContextWithUser(subject)
        };

        var requestId = Guid.NewGuid();
        var status = new Models.RequestStatus
        {
            RequestId = requestId,
            Owner = subject,
            Status = RequestStatusState.Completed
        };
        await _dbFixture.CreateRequestStatus(status);

        var result = await controller.GetRequestStatus(
            _unitOfWork,
            requestId);

        var response = (result.Result as OkObjectResult)?.Value as RequestStatusResponse;
        response.Should().NotBeNull();
        response!.Status.Should().Be(RequestStatus.Completed);
    }

    private static ControllerContext CreateContextWithUser(string subject)
    {
        return new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(new System.Security.Claims.Claim[]
                {
                    new(ClaimTypes.NameIdentifier, subject),
                })),
            }
        };
    }
}
