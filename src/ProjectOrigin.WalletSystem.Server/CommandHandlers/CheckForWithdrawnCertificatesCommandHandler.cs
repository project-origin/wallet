using System;
using System.Threading.Tasks;
using MassTransit;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.CommandHandlers;

public record CheckForWithdrawnCertificatesCommand
{
}

public class CheckForWithdrawnCertificatesCommandHandler : IConsumer<CheckForWithdrawnCertificatesCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    public CheckForWithdrawnCertificatesCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task Consume(ConsumeContext<CheckForWithdrawnCertificatesCommand> context)
    {
        var withdrawnCursor = new WithdrawnCursor { StampName = "Narnia", SyncPosition = 1, LastSyncDate = DateTimeOffset.UtcNow };
        await _unitOfWork.WithdrawnCursorRepository.InsertWithdrawnCursor(withdrawnCursor);
        _unitOfWork.Commit();
    }
}
