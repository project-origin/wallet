using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Projections;
using ProjectOrigin.WalletSystem.Server.Services;

namespace ProjectOrigin.WalletSystem.Server.CommandHandlers;

public record VerifySliceCommand(Guid Id,
                    Guid DepositEndpointId,
                    int DepositEndpointPosition,
                    string Registry,
                    Guid CertificateId,
                    long Quantity,
                    byte[] RandomR);

public class VerifySliceCommandHandler : IConsumer<VerifySliceCommand>
{
    private readonly ILogger<VerifySliceCommandHandler> _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRegistryService _registryService;

    public VerifySliceCommandHandler(ILogger<VerifySliceCommandHandler> logger, IUnitOfWork unitOfWork, IRegistryService registryService)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _registryService = registryService;
    }

    public async Task Consume(ConsumeContext<VerifySliceCommand> context)
    {
        var receivedSlice = context.Message;

        // Get Granular Certificate Projection from registry
        var getCertificateResult = await _registryService.GetGranularCertificate(receivedSlice.Registry, receivedSlice.CertificateId);

        switch (getCertificateResult)
        {
            case GetCertificateResult.Success:
                var success = (GetCertificateResult.Success)getCertificateResult;

                // Verify received slice exists on certificate
                var secretCommitmentInfo = new PedersenCommitment.SecretCommitmentInfo((uint)receivedSlice.Quantity, receivedSlice.RandomR);
                var sliceId = ByteString.CopyFrom(SHA256.HashData(secretCommitmentInfo.Commitment.C));
                var foundSlice = success.GranularCertificate.GetCertificateSlice(sliceId);
                if (foundSlice is null)
                {
                    var message = $"Slice with id {Convert.ToBase64String(sliceId.Span)} not found in certificate {receivedSlice.CertificateId}";
                    _logger.LogWarning(message);
                    return;
                }

                var depositEndpoint = await _unitOfWork.WalletRepository.GetDepositEndpoint(receivedSlice.DepositEndpointId);
                var positionPublicKey = depositEndpoint.PublicKey.Derive(receivedSlice.DepositEndpointPosition).GetPublicKey();
                if (!foundSlice.Owner.ImportKey().Equals(positionPublicKey))
                {
                    var message = $"Not correct publicKey on {receivedSlice.CertificateId}";
                    _logger.LogWarning(message);
                    return;
                }

                await InsertIntoWallet(receivedSlice, success.GranularCertificate);
                return;

            case GetCertificateResult.TransientFailure:
                var transient = (GetCertificateResult.TransientFailure)getCertificateResult;
                var transientMessage = $"Transient failed to get GranularCertificate with id {receivedSlice.CertificateId} on registry {receivedSlice.Registry}";
                _logger.LogWarning(transient.Exception, transientMessage);
                throw new TransientException(transientMessage, transient.Exception);

            case GetCertificateResult.NotFound:
                _logger.LogWarning($"GranularCertificate with id {receivedSlice.CertificateId} not found in registry {receivedSlice.Registry}");
                return;

            case GetCertificateResult.Failure:
                var failure = (GetCertificateResult.Failure)getCertificateResult;
                _logger.LogError(failure.Exception, $"Failed to get certificate with {receivedSlice.CertificateId}");
                throw new Exception($"Failed to get certificate with {receivedSlice.CertificateId}", failure.Exception);

            default:
                throw new NotSupportedException($"GetCertificateResult type {getCertificateResult.GetType()} not supported.");
        }
    }

    private async Task InsertIntoWallet(VerifySliceCommand receivedSlice, GranularCertificate certificateProjection)
    {
        var registry = await _unitOfWork.RegistryRepository.GetRegistryFromName(receivedSlice.Registry);
        if (registry is null)
        {
            registry = new RegistryModel(Guid.NewGuid(), receivedSlice.Registry);
            await _unitOfWork.RegistryRepository.InsertRegistry(registry);
        }

        var slice = new Slice(Guid.NewGuid(),
            receivedSlice.DepositEndpointId,
            receivedSlice.DepositEndpointPosition,
            registry.Id,
            receivedSlice.CertificateId,
            receivedSlice.Quantity,
            receivedSlice.RandomR,
            SliceState.Available);

        var certificate = await _unitOfWork.CertificateRepository.GetCertificate(registry.Id, slice.CertificateId);
        if (certificate == null)
        {
            var attributes = certificateProjection.Attributes
                .Select(attribute => new CertificateAttribute(attribute.Key, attribute.Value))
                .ToList();

            certificate = new Certificate(slice.CertificateId,
                registry.Id,
                certificateProjection.Period.Start.ToDateTimeOffset(),
                certificateProjection.Period.End.ToDateTimeOffset(),
                certificateProjection.GridArea,
                (GranularCertificateType)certificateProjection.Type,
                attributes);
            await _unitOfWork.CertificateRepository.InsertCertificate(certificate);
        }

        await _unitOfWork.CertificateRepository.InsertSlice(slice);

        _unitOfWork.Commit();

        _logger.LogTrace($"Slice on certificate ”{slice.CertificateId}” inserted into wallet.");
    }
}
