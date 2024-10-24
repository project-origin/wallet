using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MassTransit;
using MassTransit.Courier.Contracts;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Common.V1;
using ProjectOrigin.Electricity.V1;
using ProjectOrigin.Vault.Activities;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Services.REST.v1;
using FederatedStreamId = ProjectOrigin.Common.V1.FederatedStreamId;

namespace ProjectOrigin.Vault.CommandHandlers;

public record CheckForWithdrawnCertificatesCommand
{
}

public class CheckForWithdrawnCertificatesCommandHandler : IConsumer<CheckForWithdrawnCertificatesCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CheckForWithdrawnCertificatesCommandHandler> _logger;
    private readonly IEndpointNameFormatter _formatter;
    private readonly NetworkOptions _networkOptions;

    public CheckForWithdrawnCertificatesCommandHandler(IUnitOfWork unitOfWork,
        IOptions<NetworkOptions> networkOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<CheckForWithdrawnCertificatesCommandHandler> logger,
        IEndpointNameFormatter formatter)
    {
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _formatter = formatter;
        _networkOptions = networkOptions.Value;
    }

    public async Task Consume(ConsumeContext<CheckForWithdrawnCertificatesCommand> context)
    {
        var client = _httpClientFactory.CreateClient();
        var stamps = _networkOptions.Issuers;

        foreach (var stamp in stamps)
        {
            var cursors = await _unitOfWork.WithdrawnCursorRepository.GetWithdrawnCursors();
            var matchingCursor = cursors.FirstOrDefault(x => x.StampName == stamp.Key) ?? new WithdrawnCursor
            {
                StampName = stamp.Key,
                SyncPosition = 0,
                LastSyncDate = DateTimeOffset.UtcNow
            };

            var response = (await client.GetFromJsonAsync<ResultList<WithdrawnCertificateDto, PageInfo>>(stamp.Value.StampUrl + $"/v1/certificates/withdrawn?lastWithdrawnId={matchingCursor.SyncPosition}"))!;

            if (!response.Result.Any())
            {
                _logger.LogInformation("No withdrawn certificates found for {StampName}", stamp.Key);
                continue;
            }

            _logger.LogInformation("Found {Count} withdrawn certificates for {StampName}", response.Metadata.Count, stamp.Key);
            List<Task> tasks = new();
            foreach (var withdrawnCertificate in response.Result)
            {
                await _unitOfWork.CertificateRepository.WithdrawCertificate(withdrawnCertificate.RegistryName, withdrawnCertificate.CertificateId);
                var claimedSlices = await _unitOfWork.CertificateRepository.GetClaimedSlicesOfCertificate(withdrawnCertificate.RegistryName, withdrawnCertificate.CertificateId);
                foreach (var claimedSlice in claimedSlices)
                {
                    _logger.LogInformation("Unclaiming slice {sliceId} on certificate {registry}, {certificiateId}", claimedSlice.Id, claimedSlice.RegistryName, claimedSlice.CertificateId);
                    var routingSlip = await BuildUnclaimRoutingSlip(claimedSlice.RegistryName, claimedSlice.CertificateId, claimedSlice.Id);
                    tasks.Add(context.Execute(routingSlip));
                }
            }

            await Task.WhenAll(tasks);
            matchingCursor.SyncPosition = response.Result.Max(x => x.Id);
            matchingCursor.LastSyncDate = DateTimeOffset.UtcNow;
            await _unitOfWork.WithdrawnCursorRepository.UpdateWithdrawnCursor(matchingCursor);
            _unitOfWork.Commit();
        }
        client.Dispose();
    }

    private async Task<RoutingSlip> BuildUnclaimRoutingSlip(string registry, Guid certificateId, Guid sliceId)
    {
        var builder = new RoutingSlipBuilder(Guid.NewGuid());

        var claim = await _unitOfWork.ClaimRepository.GetClaimFromSliceId(sliceId);
        var federatedStreamId = new FederatedStreamId { Registry = registry, StreamId = new Uuid { Value = certificateId.ToString() } };
        var unclaimEvent = CreateUnclaimEvent(claim.Id);
        var sourceSlicePrivateKey = await _unitOfWork.WalletRepository.GetPrivateKeyForSlice(sliceId);
        var transaction = sourceSlicePrivateKey.SignRegistryTransaction(federatedStreamId, unclaimEvent);

        builder.AddActivity<SendRegistryTransactionActivity, SendRegistryTransactionArguments>(_formatter,
            new()
            {
                Transaction = transaction
            });

        builder.AddActivity<WaitCommittedRegistryTransactionActivity, WaitCommittedTransactionArguments>(_formatter,
            new()
            {
                CertificateId = certificateId,
                RegistryName = registry,
                SliceId = sliceId,
                TransactionId = transaction.ToShaId(),
                RequestStatusArgs = null
            });

        builder.AddActivity<UpdateSliceStateActivity, UpdateSliceStateArguments>(_formatter,
            new()
            {
                SliceStates = new Dictionary<Guid, WalletSliceState>
                {
                    { sliceId, WalletSliceState.Available }
                }
            });

        builder.AddActivity<UpdateClaimStateActivity, UpdateClaimStateArguments>(_formatter,
            new()
            {
                Id = claim.Id,
                State = ClaimState.Unclaimed,
                RequestStatusArgs = null
            });

        return builder.Build();
    }

    private static UnclaimedEvent CreateUnclaimEvent(Guid claimId)
    {
        var unclaimEvent = new UnclaimedEvent
        {
            //TODO what is this?
            AllocationId = new Uuid { Value = claimId.ToString() }
        };

        return unclaimEvent;
    }
}

public record WithdrawnCertificateDto
{
    public required int Id { get; init; }
    public required string RegistryName { get; init; }
    public required Guid CertificateId { get; init; }
    public required DateTimeOffset WithdrawnDate { get; init; }
}
