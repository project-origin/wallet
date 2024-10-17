using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using MassTransit;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProjectOrigin.Vault.Database;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.Options;
using ProjectOrigin.Vault.Services.REST.v1;

namespace ProjectOrigin.Vault.CommandHandlers;

public record CheckForWithdrawnCertificatesCommand
{
}

public class CheckForWithdrawnCertificatesCommandHandler : IConsumer<CheckForWithdrawnCertificatesCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CheckForWithdrawnCertificatesCommandHandler> _logger;
    private readonly NetworkOptions _networkOptions;
    public CheckForWithdrawnCertificatesCommandHandler(IUnitOfWork unitOfWork,
        IOptions<NetworkOptions> networkOptions,
        IHttpClientFactory httpClientFactory,
        ILogger<CheckForWithdrawnCertificatesCommandHandler> logger)
    {
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
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
            foreach (var withdrawnCertificate in response.Result)
            {
                await _unitOfWork.CertificateRepository.WithdrawCertificate(withdrawnCertificate.RegistryName, withdrawnCertificate.CertificateId);
                var claimedSlices = await _unitOfWork.CertificateRepository.GetClaimedSlicesOfCertificate(withdrawnCertificate.RegistryName, withdrawnCertificate.CertificateId);
                foreach (var claimedSlice in claimedSlices)
                {
                    //Unclaim (which is next task)
                }
            }

            matchingCursor.SyncPosition = response.Result.Max(x => x.Id);
            matchingCursor.LastSyncDate = DateTimeOffset.UtcNow;
            await _unitOfWork.WithdrawnCursorRepository.UpdateWithdrawnCursor(matchingCursor);
            _unitOfWork.Commit();
        }
        client.Dispose();
    }
}

public record WithdrawnCertificateDto
{
    public required int Id { get; init; }
    public required string RegistryName { get; init; }
    public required Guid CertificateId { get; init; }
    public required DateTimeOffset WithdrawnDate { get; init; }
}
