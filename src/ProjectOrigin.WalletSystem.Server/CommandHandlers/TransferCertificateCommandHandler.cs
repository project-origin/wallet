using System;
using System.Threading.Tasks;
using MassTransit;

namespace ProjectOrigin.WalletSystem.Server.CommandHandlers;

public record TransferCertificateCommand(string Registry, Guid CertificateId, uint Quantity, Guid Receiver);

public class TransferCertificateCommandHandler : IConsumer<TransferCertificateCommand>
{
    public Task Consume(ConsumeContext<TransferCertificateCommand> context)
    {

        throw new NotImplementedException();
    }
}
