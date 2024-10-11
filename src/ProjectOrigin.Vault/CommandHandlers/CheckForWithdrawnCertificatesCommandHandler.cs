using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
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
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly NetworkOptions _networkOptions;
    public CheckForWithdrawnCertificatesCommandHandler(IUnitOfWork unitOfWork, IOptions<NetworkOptions> networkOptions, IHttpClientFactory httpClientFactory)
    {
        _unitOfWork = unitOfWork;
        _httpClientFactory = httpClientFactory;
        _networkOptions = networkOptions.Value;
    }

    public async Task Consume(ConsumeContext<CheckForWithdrawnCertificatesCommand> context)
    {
        var stamps = _networkOptions.Stamps;

        foreach (var stamp in stamps)
        {
            var cursors = await _unitOfWork.WithdrawnCursorRepository.GetWithdrawnCursors();
            var matchingCursor = cursors.FirstOrDefault(x => x.StampName == stamp.Key) ?? new WithdrawnCursor
            {
                StampName = stamp.Key,
                SyncPosition = 0,
                LastSyncDate = DateTimeOffset.UtcNow
            };

            var client = _httpClientFactory.CreateClient();

            try
            {
                var response = (await client.GetFromJsonAsync<ResultList<WithdrawnCertificateDto,PageInfo>>(stamp.Value.Url + $"/v1/certificates/withdrawn?lastWithdrawnId={matchingCursor.SyncPosition}"))!;

                if (!response.Result.Any()) continue;

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
                await UpdateWithdrawnCursor(matchingCursor);
                _unitOfWork.Commit();
            }
            catch (Exception e)
            {
            }
        }
    }

    private async Task UpdateWithdrawnCursor(WithdrawnCursor updatedCursor)
    {
        var cursors = await _unitOfWork.WithdrawnCursorRepository.GetWithdrawnCursors();
        var matchingCursor = cursors.FirstOrDefault(x => x.StampName == updatedCursor.StampName);
        if (matchingCursor == null)
            await _unitOfWork.WithdrawnCursorRepository.InsertWithdrawnCursor(updatedCursor);
        else
            await _unitOfWork.WithdrawnCursorRepository.UpdateWithdrawnCursor(updatedCursor);
    }
}
public record ResultList<T, TPageInfo>()
{
    public required IEnumerable<T> Result { get; init; }
    public required TPageInfo Metadata { get; init; }
}

public record PageInfo()
{
    public required int Count { get; init; }
    public required int Offset { get; init; }
    public required int Limit { get; init; }
    public required int Total { get; init; }
}

public record WithdrawnCertificateDto
{
    public required int Id { get; init; }
    public required string RegistryName { get; init; }
    public required Guid CertificateId { get; init; }
    public required DateTimeOffset WithdrawnDate { get; init; }
}
