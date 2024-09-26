using System;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;

namespace ProjectOrigin.Vault.CommandHandlers;

public record CheckForWithdrawnCertificatesCommand
{
}

public class CheckForWithdrawnCertificatesCommandHandler : IConsumer<CheckForWithdrawnCertificatesCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly NetworkOptions _networkOptions;
    public CheckForWithdrawnCertificatesCommandHandler(IUnitOfWork unitOfWork, IOptions<NetworkOptions> networkOptions)
    {
        _unitOfWork = unitOfWork;
        _networkOptions = networkOptions.Value;
    }

    public async Task Consume(ConsumeContext<CheckForWithdrawnCertificatesCommand> context)
    {
        var stamps = _networkOptions.Stamps;

        var withdrawnCursor = new WithdrawnCursor { StampName = "Narnia", SyncPosition = 1, LastSyncDate = DateTimeOffset.UtcNow };
        await _unitOfWork.WithdrawnCursorRepository.InsertWithdrawnCursor(withdrawnCursor);
        _unitOfWork.Commit();
    }
}
