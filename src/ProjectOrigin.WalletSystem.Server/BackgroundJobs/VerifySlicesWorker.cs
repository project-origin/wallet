using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using ProjectOrigin.WalletSystem.Server.Database;

namespace ProjectOrigin.WalletSystem.Server.BackgroundJobs;

public class VerifySlicesWorker : BackgroundService
{
    private readonly UnitOfWork _unitOfWork;

    public VerifySlicesWorker(UnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var repository = _unitOfWork.CertificateRepository;

        var slices = await repository.GetAllReceivedSlices();
    }
}
