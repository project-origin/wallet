using System;
using System.Collections.Generic;
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
            var client = _httpClientFactory.CreateClient();

            var response = (await client.GetFromJsonAsync<WithdrawnCertificatesResponse>(stamp.Value.Url))!;
        }

        var withdrawnCursor = new WithdrawnCursor { StampName = "Narnia", SyncPosition = 1, LastSyncDate = DateTimeOffset.UtcNow };
        await _unitOfWork.WithdrawnCursorRepository.InsertWithdrawnCursor(withdrawnCursor);
        _unitOfWork.Commit();
    }
}

public record WithdrawnCertificatesResponse
{
    public required int PageSize { get; init; }
    public required int PageNumber { get; init; }
    public required List<WithdrawnCertificateDto> WithdrawnCertificates { get; init; }
}

public record WithdrawnCertificateDto
{
    public required int Id { get; init; }
    public required string RegistryName { get; init; }
    public required Guid CertificateId { get; init; }
    public required DateTimeOffset WithdrawnDate { get; init; }
}
