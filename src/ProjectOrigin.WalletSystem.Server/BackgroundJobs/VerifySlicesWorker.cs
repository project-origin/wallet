using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.WalletSystem.Server.Database;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.Projections;
using ProjectOrigin.WalletSystem.Server.Services;

namespace ProjectOrigin.WalletSystem.Server.BackgroundJobs;

public class VerifySlicesWorker : BackgroundService
{
    private readonly ILogger<VerifySlicesWorker> _logger;
    private readonly IUnitOfWorkFactory _unitOfWorkFactory;
    private readonly VerifySlicesWorkerOptions _options;
    private readonly IRegistryService _registryService;

    public VerifySlicesWorker(ILogger<VerifySlicesWorker> logger, IUnitOfWorkFactory unitOfWorkFactory, IOptions<VerifySlicesWorkerOptions> options, IRegistryService registryService)
    {
        _logger = logger;
        _unitOfWorkFactory = unitOfWorkFactory;
        _options = options.Value;
        _registryService = registryService;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation($"{nameof(VerifySlicesWorker)} is working.");

        while (!stoppingToken.IsCancellationRequested)
        {
            using var unitOfWork = _unitOfWorkFactory.Create();
            try
            {
                var receivedSlice = await unitOfWork.CertificateRepository.GetTop1ReceivedSlice();
                if (receivedSlice is not null)
                {
                    await ProcessReceivedSlice(unitOfWork, receivedSlice);
                }
                else
                {
                    _logger.LogTrace("No received slices found, sleeping.");
                    await Task.Delay(_options.SleepTime, stoppingToken);
                }
            }
            catch (Exception ex)
            {
                unitOfWork.Rollback();
                _logger.LogError("VerifySlicesWorker failed. Error: {ex}", ex);
            }
        }
    }

    private async Task ProcessReceivedSlice(UnitOfWork unitOfWork, ReceivedSlice receivedSlice)
    {
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
                    _logger.LogWarning($"Slice with id {sliceId} not found in certificate {receivedSlice.CertificateId}. Deleting received slice.");
                    await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
                    unitOfWork.Commit();
                    return;
                }

                await InsertIntoWallet(unitOfWork, receivedSlice, success.GranularCertificate);
                return;

            case GetCertificateResult.TransientFailure:
                var transient = (GetCertificateResult.Failure)getCertificateResult;
                _logger.LogWarning(transient.Exception, $"Transient failed to get GranularCertificate with id {receivedSlice.CertificateId} on registry {receivedSlice.Registry}. Sleeping.");
                await Task.Delay(_options.SleepTime);
                return;

            case GetCertificateResult.NotFound:
                _logger.LogWarning($"GranularCertificate with id {receivedSlice.CertificateId} not found in registry {receivedSlice.Registry}. Deleting received slice.");
                await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
                unitOfWork.Commit();
                return;

            case GetCertificateResult.Failure:
                var failure = (GetCertificateResult.Failure)getCertificateResult;
                _logger.LogError(failure.Exception, $"Failed to get certificate with {receivedSlice.CertificateId}, Deleting received slice.");
                await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);
                unitOfWork.Commit();
                return;
        }
    }

    private async Task InsertIntoWallet(UnitOfWork unitOfWork, ReceivedSlice receivedSlice, GranularCertificate certificateProjection)
    {
        var registry = await unitOfWork.RegistryRepository.GetRegistryFromName(receivedSlice.Registry);
        if (registry is null)
        {
            registry = new RegistryModel(Guid.NewGuid(), receivedSlice.Registry);
            await unitOfWork.RegistryRepository.InsertRegistry(registry);
        }

        var slice = new Slice(Guid.NewGuid(),
            receivedSlice.DepositEndpointId,
            receivedSlice.DepositEndpointPosition,
            registry.Id,
            receivedSlice.CertificateId,
            receivedSlice.Quantity,
            receivedSlice.RandomR);

        var certificate = await unitOfWork.CertificateRepository.GetCertificate(registry.Id, slice.CertificateId);
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
            await unitOfWork.CertificateRepository.InsertCertificate(certificate);
        }

        await unitOfWork.CertificateRepository.InsertSlice(slice);
        await unitOfWork.CertificateRepository.RemoveReceivedSlice(receivedSlice);

        unitOfWork.Commit();

        _logger.LogTrace($"Slice on certificate ”{slice.CertificateId}” inserted into wallet.");
    }
}
