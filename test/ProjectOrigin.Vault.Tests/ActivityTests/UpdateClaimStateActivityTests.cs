using MassTransit;
using Microsoft.Extensions.Logging;
using Npgsql;
using NSubstitute;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Metrics;
using ProjectOrigin.Vault.Models;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProjectOrigin.Vault.Tests.ActivityTests;

public class UpdateClaimStateActivityTests
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly UpdateClaimStateActivity _activity;
    private readonly ExecuteContext<UpdateClaimStateArguments> _context;
    private readonly IClaimMetrics _claimsMetrics;

    public UpdateClaimStateActivityTests()
    {
        _unitOfWork = Substitute.For<IUnitOfWork>();
        _claimsMetrics = Substitute.For<IClaimMetrics>();

        _activity = new UpdateClaimStateActivity(
            _unitOfWork,
            Substitute.For<ILogger<UpdateClaimStateActivity>>(),
            _claimsMetrics
        );
        _context = Substitute.For<ExecuteContext<UpdateClaimStateArguments>>();
    }

    [Fact]
    public async Task Execute_WhenCalledWithValidArguments_ShouldComplete()
    {
        // Arrange
        var claimId = Guid.NewGuid();
        var quantity = 150;
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = claimId,
            State = ClaimState.Claimed,
            RequestStatusArgs = new RequestStatusArgs
            {
                RequestId = claimId,
                Owner = Guid.NewGuid().ToString(),
                RequestStatusType = RequestStatusType.Claim
            }
        });
        _unitOfWork.ClaimRepository.GetClaimWithQuantity(Arg.Is(claimId)).Returns(new ClaimWithQuantity
        {
            Id = claimId,
            ConsumptionSliceId = Guid.NewGuid(),
            ProductionSliceId = Guid.NewGuid(),
            Quantity = quantity,
            State = ClaimState.Claimed,
            IsTrialClaim = false
        });

        // Act
        await _activity.Execute(_context);

        // Assert
        await _unitOfWork.ClaimRepository.Received(1).SetClaimState(Arg.Is(_context.Arguments.Id), Arg.Is(_context.Arguments.State));
        _unitOfWork.Received(1).Commit();
        _context.Received(1).Completed();
    }

    [Fact]
    public async Task Execute_WhenSuccessfullyClaimed_ShouldCallIncrementClaimsClaimedCounterMethod()
    {
        // Arrange
        var claimId = Guid.NewGuid();
        var quantity = 150;
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = claimId,
            State = ClaimState.Claimed,
            RequestStatusArgs = new RequestStatusArgs
            {
                RequestId = claimId,
                Owner = Guid.NewGuid().ToString(),
                RequestStatusType = RequestStatusType.Claim
            }
        });
        _unitOfWork.ClaimRepository.GetClaimWithQuantity(Arg.Is(claimId)).Returns(new ClaimWithQuantity
        {
            Id = claimId,
            ConsumptionSliceId = Guid.NewGuid(),
            ProductionSliceId = Guid.NewGuid(),
            Quantity = quantity,
            State = ClaimState.Claimed,
            IsTrialClaim = false
        });

        // Act
        await _activity.Execute(_context);
        await _unitOfWork.ClaimRepository.Received(1).SetClaimState(Arg.Is(_context.Arguments.Id), Arg.Is(_context.Arguments.State));
        _unitOfWork.Received(1).Commit();
        _context.Received(1).Completed();

        // Assert
        _claimsMetrics.Received(1).IncrementClaimed();
    }

    [Fact]
    public async Task Execute_WhenSettingStateToClaimed_ExpectIncrementTotalWattsClaimed()
    {
        // Arrange
        var claimId = Guid.NewGuid();
        var quantity = 150;
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = claimId,
            State = ClaimState.Claimed,
            RequestStatusArgs = new RequestStatusArgs
            {
                RequestId = claimId,
                Owner = Guid.NewGuid().ToString(),
                RequestStatusType = RequestStatusType.Claim
            }
        });
        _unitOfWork.ClaimRepository.GetClaimWithQuantity(Arg.Is(claimId)).Returns(new ClaimWithQuantity
        {
            Id = claimId,
            ConsumptionSliceId = Guid.NewGuid(),
            ProductionSliceId = Guid.NewGuid(),
            Quantity = quantity,
            State = ClaimState.Claimed,
            IsTrialClaim = false
        });

        // Act
        await _activity.Execute(_context);
        await _unitOfWork.ClaimRepository.Received(1).SetClaimState(Arg.Is(_context.Arguments.Id), Arg.Is(_context.Arguments.State));
        _unitOfWork.Received(1).Commit();
        _context.Received(1).Completed();

        // Assert
        _claimsMetrics.Received(1).IncrementTotalWattsClaimed(quantity);
    }

    [Fact]
    public async Task Execute_WhenSettingStateToUnclaimed_ExpectDecreaseTotalWattsClaimed()
    {
        // Arrange
        var claimId = Guid.NewGuid();
        var quantity = 150;
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = claimId,
            State = ClaimState.Unclaimed,
            RequestStatusArgs = new RequestStatusArgs
            {
                RequestId = claimId,
                Owner = Guid.NewGuid().ToString(),
                RequestStatusType = RequestStatusType.Claim
            }
        });
        _unitOfWork.ClaimRepository.GetClaimWithQuantity(Arg.Is(claimId)).Returns(new ClaimWithQuantity
        {
            Id = claimId,
            ConsumptionSliceId = Guid.NewGuid(),
            ProductionSliceId = Guid.NewGuid(),
            Quantity = quantity,
            State = ClaimState.Unclaimed,
            IsTrialClaim = false
        });

        // Act
        await _activity.Execute(_context);
        await _unitOfWork.ClaimRepository.Received(1).SetClaimState(Arg.Is(_context.Arguments.Id), Arg.Is(_context.Arguments.State));
        _unitOfWork.Received(1).Commit();
        _context.Received(1).Completed();

        // Assert
        _claimsMetrics.Received(1).IncrementTotalWattsClaimed(-quantity);
    }

    [Fact]
    public async Task Execute_WhenSettingStateToClaimedAndTrialClaim_ExpectIncrementTotalTrailWattsClaimed()
    {
        // Arrange
        var claimId = Guid.NewGuid();
        var quantity = 150;
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = claimId,
            State = ClaimState.Claimed,
            RequestStatusArgs = new RequestStatusArgs
            {
                RequestId = claimId,
                Owner = Guid.NewGuid().ToString(),
                RequestStatusType = RequestStatusType.Claim
            }
        });
        _unitOfWork.ClaimRepository.GetClaimWithQuantity(Arg.Is(claimId)).Returns(new ClaimWithQuantity
        {
            Id = claimId,
            ConsumptionSliceId = Guid.NewGuid(),
            ProductionSliceId = Guid.NewGuid(),
            Quantity = quantity,
            State = ClaimState.Claimed,
            IsTrialClaim = true
        });

        // Act
        await _activity.Execute(_context);
        await _unitOfWork.ClaimRepository.Received(1).SetClaimState(Arg.Is(_context.Arguments.Id), Arg.Is(_context.Arguments.State));
        _unitOfWork.Received(1).Commit();
        _context.Received(1).Completed();

        // Assert
        _claimsMetrics.Received(1).IncrementTotalTrialWattsClaimed(quantity);
    }

    [Fact]
    public async Task Execute_WhenSettingStateToUnclaimedAndTrialClaim_ExpectDecreaseTotalTrailWattsClaimed()
    {
        // Arrange
        var claimId = Guid.NewGuid();
        var quantity = 150;
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = claimId,
            State = ClaimState.Unclaimed,
            RequestStatusArgs = new RequestStatusArgs
            {
                RequestId = claimId,
                Owner = Guid.NewGuid().ToString(),
                RequestStatusType = RequestStatusType.Claim
            }
        });
        _unitOfWork.ClaimRepository.GetClaimWithQuantity(Arg.Is(claimId)).Returns(new ClaimWithQuantity
        {
            Id = claimId,
            ConsumptionSliceId = Guid.NewGuid(),
            ProductionSliceId = Guid.NewGuid(),
            Quantity = quantity,
            State = ClaimState.Unclaimed,
            IsTrialClaim = true
        });

        // Act
        await _activity.Execute(_context);
        await _unitOfWork.ClaimRepository.Received(1).SetClaimState(Arg.Is(_context.Arguments.Id), Arg.Is(_context.Arguments.State));
        _unitOfWork.Received(1).Commit();
        _context.Received(1).Completed();

        // Assert
        _claimsMetrics.Received(1).IncrementTotalTrialWattsClaimed(-quantity);
    }

    [Fact]
    public async Task Execute_WhenSetClaimThrowsPostgresException_ShouldThrowTransientException()
    {
        // Arrange
        var exceptionToBeThrown = new PostgresException("", "", "", "");
        _context.Arguments.Returns(new UpdateClaimStateArguments()
        {
            Id = Guid.NewGuid(),
            State = ClaimState.Claimed,
            RequestStatusArgs = new RequestStatusArgs
            {
                RequestId = Guid.NewGuid(),
                Owner = Guid.NewGuid().ToString(),
                RequestStatusType = RequestStatusType.Claim
            }
        });
        _unitOfWork.ClaimRepository.When(x => x.SetClaimState(Arg.Any<Guid>(), Arg.Any<ClaimState>())).Do(x => throw exceptionToBeThrown);

        // Act
        await Assert.ThrowsAsync<TransientException>(async () => await _activity.Execute(_context));

        // Assert
        _context.DidNotReceive().Completed();
    }
}
