using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using Xunit;

namespace ProjectOrigin.Vault.Tests.ActivityTests;

public class UpdateClaimStateActivityTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateClaimStateActivity _activity;
    private readonly ExecuteContext<UpdateClaimStateArguments> _context;

    public UpdateClaimStateActivityTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _activity = new UpdateClaimStateActivity(
            _unitOfWork,
            Substitute.For<ILogger<UpdateClaimStateActivity>>()
        );
        _context = Substitute.For<ExecuteContext<UpdateClaimStateArguments>>();
    }

    [Fact]
    public async Task Execute_WhenCalledWithValidArguments_ShouldComplete()
    {
        // Arrange
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = Guid.NewGuid(),
            State = ClaimState.Claimed,
            RequestId = Guid.NewGuid(),
            Owner = Guid.NewGuid().ToString()
        });

        // Act
        await _activity.Execute(_context);

        // Assert
        await _unitOfWork.Received(1).ClaimRepository.SetClaimState(Arg.Is(_context.Arguments.Id), Arg.Is(_context.Arguments.State));
        _unitOfWork.Received(1).Commit();
        _context.Received(1).Completed();
    }

    [Fact]
    public async Task Execute_WhenSetClaimThrowsException_ShouldThrowException()
    {
        // Arrange
        var exceptionToBeThrown = new Exception();
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = Guid.NewGuid(),
            State = ClaimState.Claimed,
            RequestId = Guid.NewGuid(),
            Owner = Guid.NewGuid().ToString()
        });
        _unitOfWork.ClaimRepository.When(x => x.SetClaimState(Arg.Any<Guid>(), Arg.Any<ClaimState>())).Do(x => throw exceptionToBeThrown);

        // Act
        await _activity.Execute(_context);

        // Assert
        _context.Received(1).Faulted(Arg.Is(exceptionToBeThrown));
        _unitOfWork.Received(1).Rollback();
        _unitOfWork.DidNotReceive().Commit();
        _context.DidNotReceive().Completed();
    }
}
