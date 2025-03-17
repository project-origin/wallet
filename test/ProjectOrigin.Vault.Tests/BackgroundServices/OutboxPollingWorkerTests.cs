using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using ProjectOrigin.Vault.CommandHandlers;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Jobs;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Repositories;
using Xunit;

namespace ProjectOrigin.Vault.Tests.BackgroundServices;

public class OutboxPollingWorkerTests
{
    private readonly IServiceScope _scopeMock = Substitute.For<IServiceScope>();
    private readonly IServiceScopeFactory _scopeFactoryMock = Substitute.For<IServiceScopeFactory>();
    private readonly IServiceProvider _serviceProviderMock = Substitute.For<IServiceProvider>();
    private readonly IUnitOfWork _unitOfWorkMock = Substitute.For<IUnitOfWork>();
    private readonly IBus _busMock = Substitute.For<IBus>();
    private readonly ILogger<OutboxPollingWorker> _loggerMock = Substitute.For<ILogger<OutboxPollingWorker>>();
    private readonly IOutboxMessageRepository _outboxRepositoryMock = Substitute.For<IOutboxMessageRepository>();
    private readonly OutboxPollingWorker _sut;

    public OutboxPollingWorkerTests()
    {
        _serviceProviderMock.GetService<IUnitOfWork>().Returns(_unitOfWorkMock);
        _serviceProviderMock.GetService<IBus>().Returns(_busMock);
        _scopeMock.ServiceProvider.Returns(_serviceProviderMock);
        _scopeFactoryMock.CreateScope().Returns(_scopeMock);
        _serviceProviderMock.GetService<IServiceScopeFactory>().Returns(_scopeFactoryMock);
        _serviceProviderMock.CreateScope().Returns(_scopeMock);

        _sut = new OutboxPollingWorker(_serviceProviderMock, _loggerMock);
    }

    [Fact]
    public async Task ShouldPublishAndDeleteMessages()
    {
        var payloadObj = new ClaimCertificateCommand
        {
            Owner = Guid.NewGuid().ToString(),
            ClaimId = Guid.NewGuid(),
            ConsumptionRegistry = "energyorigin",
            ConsumptionCertificateId = Guid.NewGuid(),
            ProductionRegistry = $"energyorigin",
            ProductionCertificateId = Guid.NewGuid(),
            Quantity = 100
        };

        var message = new OutboxMessage
        {
            Created = DateTimeOffset.Now.ToUtcTime(),
            JsonPayload = JsonSerializer.Serialize(payloadObj),
            MessageType = typeof(ClaimCertificateCommand).ToString(),
            Id = Guid.NewGuid()
        };
        using var tokenSource = new CancellationTokenSource();
        _outboxRepositoryMock.GetFirst().Returns(message);
        _unitOfWorkMock.OutboxMessageRepository.Returns(_outboxRepositoryMock);
        _busMock
            .Publish(Arg.Any<object?>()!, Arg.Any<CancellationToken>())
            .ReturnsForAnyArgs(Task.CompletedTask)
            .AndDoes(_ => tokenSource.Cancel());

        // Act
        await _sut.StartAsync(tokenSource.Token);

        // Assert
        await _busMock.Received(1).Publish(Arg.Any<object?>()!, Arg.Any<CancellationToken>());
        await _outboxRepositoryMock.Received(1).Delete(message.Id);
        _unitOfWorkMock.Received(1).Commit();
        _unitOfWorkMock.DidNotReceive().Rollback();
    }

    [Fact]
    public async Task WhenMessageIsNull_ShouldNotPublishAndDelete()
    {
        using var tokenSource = new CancellationTokenSource();
        _outboxRepositoryMock.GetFirst()
            .Returns((OutboxMessage)null!)
            .AndDoes(_ => tokenSource.Cancel());
        _unitOfWorkMock.OutboxMessageRepository.Returns(_outboxRepositoryMock);

        // Act and ignore TaskCanceledException when Delay happens
        try
        {
            await _sut.StartAsync(tokenSource.Token);
        }
        catch (TaskCanceledException)
        {
        }

        // Assert
        await _busMock.DidNotReceive().Publish(Arg.Any<object?>()!, Arg.Any<CancellationToken>());
        await _outboxRepositoryMock.DidNotReceive().Delete(Arg.Any<Guid>());
        _unitOfWorkMock.DidNotReceive().Commit();
        _unitOfWorkMock.DidNotReceive().Rollback();
    }
}
