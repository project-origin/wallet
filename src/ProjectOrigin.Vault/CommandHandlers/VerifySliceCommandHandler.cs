using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Google.Protobuf;
using MassTransit;
using Microsoft.Extensions.Logging;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Projections;
using ProjectOrigin.Vault.Services;

namespace ProjectOrigin.Vault.CommandHandlers;

public record VerifySliceCommand
{
    public required Guid Id { get; init; }
    public required Guid WalletId { get; init; }
    public required Guid WalletEndpointId { get; init; }
    public required int WalletEndpointPosition { get; init; }
    public required string Registry { get; init; }
    public required Guid CertificateId { get; init; }
    public required long Quantity { get; init; }
    public required byte[] RandomR { get; init; }
    public List<WalletAttribute> HashedAttributes { get; init; } = new();
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
        _logger.LogInformation("Getting certificate {registry}, {certificateId} from registry", receivedSlice.Registry, receivedSlice.CertificateId);
        var getCertificateResult = await _registryService.GetGranularCertificate(receivedSlice.Registry, receivedSlice.CertificateId);

        _logger.LogInformation("Got certificate {registry}, {certificateId} from registry", receivedSlice.Registry, receivedSlice.CertificateId);
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

                var endpoint = await _unitOfWork.WalletRepository.GetWalletEndpoint(receivedSlice.WalletEndpointId);
                var positionPublicKey = endpoint.PublicKey.Derive(receivedSlice.WalletEndpointPosition).GetPublicKey();
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

    private async Task InsertIntoWallet(VerifySliceCommand receivedSlice, GranularCertificate registryCertificateProjection)
    {
        _logger.LogInformation("Inserting slice on certificate ”{certificateId}” into wallet.", receivedSlice.CertificateId);

        var slice = new WalletSlice
        {
            Id = Guid.NewGuid(),
            WalletEndpointId = receivedSlice.WalletEndpointId,
            WalletEndpointPosition = receivedSlice.WalletEndpointPosition,
            RegistryName = receivedSlice.Registry,
            CertificateId = receivedSlice.CertificateId,
            Quantity = receivedSlice.Quantity,
            RandomR = receivedSlice.RandomR,
            State = WalletSliceState.Available
        };

        var certificate = await _unitOfWork.CertificateRepository.GetCertificate(slice.RegistryName, slice.CertificateId);
        if (certificate == null)
        {
            var attributes = registryCertificateProjection.Attributes
                .Select(attribute => new CertificateAttribute
                {
                    Key = attribute.Key,
                    Value = attribute.Value,
                    Type = (CertificateAttributeType)attribute.Type,
                })
                .ToList();

            certificate = new Certificate
            {
                Id = slice.CertificateId,
                RegistryName = receivedSlice.Registry,
                StartDate = registryCertificateProjection.Period.Start.ToDateTimeOffset(),
                EndDate = registryCertificateProjection.Period.End.ToDateTimeOffset(),
                GridArea = registryCertificateProjection.GridArea,
                CertificateType = (GranularCertificateType)registryCertificateProjection.Type,
                Attributes = attributes,
                Withdrawn = registryCertificateProjection.Withdrawn
            };
            await _unitOfWork.CertificateRepository.InsertCertificate(certificate);
        }

        foreach (var hashedAttribute in receivedSlice.HashedAttributes)
        {
            if (registryCertificateProjection.HasHashedAttribute(hashedAttribute.Key, hashedAttribute.GetHashedValue()))
            {
                await _unitOfWork.CertificateRepository.InsertWalletAttribute(receivedSlice.WalletId, hashedAttribute);
            }
            else
            {
                _logger.LogWarning("Hashed Attribute {attributeKey} not found on certificate {certificateId}", hashedAttribute.Key, receivedSlice.CertificateId);
            }
        }

        await _unitOfWork.CertificateRepository.InsertWalletSlice(slice);

        _unitOfWork.Commit();

        _logger.LogInformation("Slice on certificate with id {certificateId} inserted into wallet.", slice.CertificateId);
    }
}
