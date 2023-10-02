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

public record VerifySliceCommand
{
    public required Guid Id { get; init; }
    public required Guid DepositEndpointId { get; init; }
    public required int DepositEndpointPosition { get; init; }
    public required string Registry { get; init; }
    public required Guid CertificateId { get; init; }
    public required long Quantity { get; init; }
    public required byte[] RandomR { get; init; }
}

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
                    _logger.LogWarning("Slice with id {sliceId} not found in certificate {certificateId}", Convert.ToBase64String(sliceId.Span), receivedSlice.CertificateId);
                    return;
                }

                var endpoint = await _unitOfWork.WalletRepository.GetReceiveEndpoint(receivedSlice.DepositEndpointId);
                var positionPublicKey = endpoint.PublicKey.Derive(receivedSlice.DepositEndpointPosition).GetPublicKey();
                if (!foundSlice.Owner.ImportKey().Equals(positionPublicKey))
                {
                    _logger.LogWarning("Not correct publicKey on {certificateId}", receivedSlice.CertificateId);
                    return;
                }

                await InsertIntoWallet(receivedSlice, success.GranularCertificate);
                return;

            case GetCertificateResult.TransientFailure:
                var transient = (GetCertificateResult.TransientFailure)getCertificateResult;
                _logger.LogWarning(transient.Exception, "Transient failed to get GranularCertificate with id {certificateId} on registry {registryName}", receivedSlice.CertificateId, receivedSlice.Registry);
                throw new TransientException($"Transient failed to get GranularCertificate with id {receivedSlice.CertificateId} on registry {receivedSlice.Registry}", transient.Exception);

            case GetCertificateResult.NotFound:
                _logger.LogWarning("GranularCertificate with id {certificateId} not found in registry {registryName}", receivedSlice.CertificateId, receivedSlice.Registry);
                return;

            case GetCertificateResult.Failure:
                var failure = (GetCertificateResult.Failure)getCertificateResult;
                _logger.LogError(failure.Exception, "Failed to get certificate with {certificateId}", receivedSlice.CertificateId);
                throw new Exception($"Failed to get certificate with {receivedSlice.CertificateId}", failure.Exception);

            default:
                throw new NotSupportedException($"GetCertificateResult type {getCertificateResult.GetType()} not supported.");
        }
    }

    private async Task InsertIntoWallet(VerifySliceCommand receivedSlice, GranularCertificate certificateProjection)
    {
        var slice = new Slice
        {
            Id = Guid.NewGuid(),
            DepositEndpointId = receivedSlice.DepositEndpointId,
            DepositEndpointPosition = receivedSlice.DepositEndpointPosition,
            Registry = receivedSlice.Registry,
            CertificateId = receivedSlice.CertificateId,
            Quantity = receivedSlice.Quantity,
            RandomR = receivedSlice.RandomR,
            SliceState = SliceState.Available
        };

        var certificate = await _unitOfWork.CertificateRepository.GetCertificate(slice.Registry, slice.CertificateId);
        if (certificate == null)
        {
            var attributes = certificateProjection.Attributes
                .Select(attribute => new CertificateAttribute
                {
                    Key = attribute.Key,
                    Value = attribute.Value
                })
                .ToList();

            certificate = new Certificate
            {
                Id = slice.CertificateId,
                Registry = receivedSlice.Registry,
                StartDate = certificateProjection.Period.Start.ToDateTimeOffset(),
                EndDate = certificateProjection.Period.End.ToDateTimeOffset(),
                GridArea = certificateProjection.GridArea,
                CertificateType = (GranularCertificateType)certificateProjection.Type,
                Attributes = attributes
            };
            await _unitOfWork.CertificateRepository.InsertCertificate(certificate);
        }

        await _unitOfWork.CertificateRepository.InsertSlice(slice);

        _unitOfWork.Commit();

        _logger.LogTrace("Slice on certificate ”{certificateId}” inserted into wallet.", slice.CertificateId);
    }
}
