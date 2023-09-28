using System;
using System.Threading;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.CommandHandlers;
using ProjectOrigin.WalletSystem.Server.Database;

namespace ProjectOrigin.WalletSystem.Server.BackgroundJobs;

public class VerifySlicesWorker : BackgroundService
{
    private static readonly TimeSpan SleepTime = TimeSpan.FromHours(1);

    private readonly ILogger<VerifySlicesWorker> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBus _bus;

    public VerifySlicesWorker(ILogger<VerifySlicesWorker> logger, IUnitOfWork unitOfWork, IBus bus)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _bus = bus;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("VerifySlicesWorker moving all receivedSlices to messageBroker.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var receivedSlice = await _unitOfWork.CertificateRepository.GetTop1ReceivedSlice();
                if (receivedSlice is not null)
                {
                    var command = new VerifySliceCommand
                    {
                        Id = receivedSlice.Id,
                        DepositEndpointId = receivedSlice.DepositEndpointId,
                        DepositEndpointPosition = receivedSlice.DepositEndpointPosition,
                        Registry = receivedSlice.Registry,
                        CertificateId = receivedSlice.CertificateId,
                        Quantity = receivedSlice.Quantity,
                        RandomR = receivedSlice.RandomR,
                    };
                    await _bus.Publish(command);
                    await _unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
                    _unitOfWork.Commit(); // unit of work is automatically reset after commit
                }
                else
                {
                    _logger.LogTrace("No received slices found, sleeping.");
                    await Task.Delay(SleepTime, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                _unitOfWork.Rollback();
                _logger.LogError("VerifySlicesWorker failed. Error: {ex}", ex);
            }
        }
    }
}
