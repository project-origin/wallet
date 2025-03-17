using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Vault.Database;

namespace ProjectOrigin.Vault.Jobs;

public class OutboxPollingWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPollingWorker> _logger;

    public OutboxPollingWorker(IServiceProvider serviceProvider, ILogger<OutboxPollingWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {

        while (!stoppingToken.IsCancellationRequested)
        {
            using var scope = _serviceProvider.CreateScope();
            var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var bus = scope.ServiceProvider.GetRequiredService<IBus>();

            var msg = await unitOfWork.OutboxMessageRepository.GetFirst();

            if (msg != null)
            {
                try
                {
                    _logger.LogInformation("Processing outbox message {msgId}.", msg.Id);
                    var type = Type.GetType($"{msg.MessageType}, ProjectOrigin.Vault");
                    var loadedObject = JsonSerializer.Deserialize(msg.JsonPayload, type!);

                    await bus.Publish(loadedObject!, stoppingToken);
                    await unitOfWork.OutboxMessageRepository.Delete(msg.Id);
                    unitOfWork.Commit();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while processing outbox message.");
                    unitOfWork.Rollback();
                }
            }
            else
                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken);
        }
    }
}
