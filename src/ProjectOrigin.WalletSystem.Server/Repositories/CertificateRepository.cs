using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class CertificateRepository : ICertificateRepository
{
    private IDbConnection _connection;

    public CertificateRepository(IDbConnection connection) => this._connection = connection;

    public async Task InsertSlice(Slice newSlice)
    {
        var registryId = await _connection.QuerySingleAsync<Guid>("SELECT Id FROM Registries WHERE Name = @registry", new { newSlice.Registry });

        await _connection.ExecuteAsync(@"INSERT INTO Slices(Id, DepositEndpointId, DepositEndpointPosition, RegistryId, CertificateId, Quantity, RandomR, SliceState) VALUES (@id, @depositEndpointId, @depositEndpointPosition, @registryId, @certificateId, @quantity, @randomR, @sliceState)", new { newSlice.Id, newSlice.DepositEndpointId, newSlice.DepositEndpointPosition, registryId, newSlice.CertificateId, newSlice.Quantity, newSlice.RandomR, newSlice.SliceState });
    }
    public async Task InsertCertificate(Certificate certificate)
    {
        var registryId = await _connection.QuerySingleOrDefaultAsync<Guid?>("SELECT Id FROM Registries WHERE Name = @registry", new { certificate.Registry });

        if (!registryId.HasValue)
        {
            registryId = Guid.NewGuid();
            await _connection.ExecuteAsync(@"INSERT INTO Registries(Id, Name) VALUES (@registryId, @registry)", new { registryId, certificate.Registry });
        }

        await _connection.ExecuteAsync(@"INSERT INTO Certificates(Id, RegistryId, StartDate, EndDate, GridArea, CertificateType) VALUES (@id, @registryId, @startDate, @endDate, @gridArea, @certificateType)",
            new { certificate.Id, registryId, startDate = certificate.StartDate.ToUtcTime(), endDate = certificate.EndDate.ToUtcTime(), certificate.GridArea, certificate.CertificateType });

        foreach (var atr in certificate.Attributes)
        {
            var atrId = Guid.NewGuid();
            await _connection.ExecuteAsync(@"INSERT INTO Attributes(Id, KeyAtr, ValueAtr, CertificateId, RegistryId) VALUES (@atrId, @key, @value, @id, @registryId)",
                new { atrId, atr.Key, atr.Value, certificate.Id, registryId });
        }
    }

    public async Task<Certificate?> GetCertificate(string registryName, Guid certificateId)
    {
        var sql = @"SELECT c.Id, r.Name as Registry, c.StartDate, c.EndDate, c.GridArea, c.CertificateType, a.Id AS AttributeId, a.KeyAtr AS Key, a.ValueAtr as Value
                    FROM Certificates c
                    INNER JOIN Registries r ON c.RegistryId = r.Id
                    LEFT JOIN Attributes a ON c.Id = a.CertificateId AND c.RegistryId = a.RegistryId
                    WHERE c.Id = @certificateId AND r.Name = @registryName";

        var certsDictionary = new Dictionary<Guid, Certificate>();
        var res = await _connection.QueryAsync<Certificate?, CertificateAttribute, Certificate?>(sql,
            (cert, atr) =>
            {
                if (cert == null) return null;

                if (!certsDictionary.TryGetValue(cert.Id, out var certificate))
                {
                    certsDictionary.Add(cert.Id, certificate = cert);
                }

                if (atr != null)
                    certificate.Attributes.Add(atr);

                return certificate;
            }, splitOn: "AttributeId", param: new { certificateId, registryName });

        return certsDictionary.Values.FirstOrDefault();
    }

    public async Task<IEnumerable<CertificateViewModel>> GetAllOwnedCertificates(string owner)
    {
        var sql = $@"SELECT c.Id, r.Name as Registry, c.StartDate, c. EndDate, c.GridArea, c.CertificateType, s.Id AS SliceId, s.Quantity as Quantity, a.Id AS AttributeId, a.KeyAtr AS Key, a.ValueAtr as Value
                    FROM Wallets w
                    JOIN DepositEndpoints de ON w.Id = de.WalletId
                    JOIN Slices s ON de.Id = s.DepositEndpointId
                    JOIN Certificates c ON s.CertificateId = c.Id
                    LEFT JOIN Attributes a ON c.Id = a.CertificateId AND c.RegistryId = a.RegistryId
                    JOIN Registries r ON c.RegistryId = r.Id
                    WHERE w.Owner = @owner AND s.SliceState = {(int)SliceState.Available}";

        var certsDictionary = new Dictionary<Guid, CertificateViewModel>();
        var res = await _connection.QueryAsync<CertificateViewModel, SliceViewModel, CertificateAttribute, CertificateViewModel>(sql,
            (cert, slice, atr) =>
            {
                if (!certsDictionary.TryGetValue(cert.Id, out var certificate))
                {
                    certsDictionary.Add(cert.Id, certificate = cert);
                }

                if (slice != null && !certificate.Slices.Contains(slice))
                    certificate.Slices.Add(slice);

                if (atr != null && !certificate.Attributes.Contains(atr))
                    certificate.Attributes.Add(atr);

                return certificate;
            },
            splitOn: "SliceId, AttributeId",
            param: new { owner });

        return certsDictionary.Values;
    }

    public Task<ReceivedSlice?> GetTop1ReceivedSlice()
    {
        return _connection.QueryFirstOrDefaultAsync<ReceivedSlice?>("SELECT * FROM ReceivedSlices LIMIT 1 FOR UPDATE");
    }

    public Task RemoveReceivedSlice(ReceivedSlice receivedSlice)
    {
        return _connection.ExecuteAsync("DELETE FROM ReceivedSlices WHERE Id = @id", new { receivedSlice.Id });
    }

    public Task<IEnumerable<Slice>> GetOwnerAvailableSlices(string registryName, Guid certificateId, string owner)
    {
        var sql = $@"SELECT s.*, r.Name as Registry
                    FROM Certificates c
                    INNER JOIN Slices s on c.Id = s.CertificateId
                    INNER JOIN Registries r on s.RegistryId = r.Id
                    INNER JOIN DepositEndpoints de on s.DepositEndpointId = de.Id
                    INNER JOIN Wallets w on de.WalletId = w.Id
                    WHERE r.Name = @registryName
                    AND s.CertificateId = @certificateId
                    AND w.owner = @owner
                    AND s.SliceState = {(int)SliceState.Available}";

        return _connection.QueryAsync<Slice>(sql, new { registryName, certificateId, owner });
    }

    public Task<long> GetToBeAvailable(string registryName, Guid certificateId, string owner)
    {
        var sql = $@"SELECT SUM(s.quantity)
                    FROM Certificates c
                    INNER JOIN Slices s on c.Id = s.CertificateId
                    INNER JOIN Registries r on s.RegistryId = r.Id
                    INNER JOIN DepositEndpoints de on s.DepositEndpointId = de.Id
                    INNER JOIN Wallets w on de.WalletId = w.Id
                    WHERE r.Name = @registryName
                    AND s.CertificateId = @certificateId
                    AND w.owner = @owner
                    AND (s.SliceState = {(int)SliceState.Available} OR s.SliceState = {(int)SliceState.Registering})";

        return _connection.QuerySingleAsync<long>(sql, new { registryName, certificateId, owner });
    }

    public Task<Slice> GetSlice(Guid sliceId)
    {
        var sql = @"SELECT s.*, r.Name as Registry
                    FROM Slices s
                    INNER JOIN Registries r ON s.RegistryId = r.Id
                    WHERE s.Id = @sliceId";

        return _connection.QuerySingleAsync<Slice>(sql, new { sliceId });
    }

    public Task SetSliceState(Guid sliceId, SliceState state)
    {
        return _connection.ExecuteAsync("UPDATE Slices SET SliceState = @state WHERE Id = @sliceId", new { sliceId, state });
    }

    /// <summary>
    /// Reserves the requested quantity of slices of the given certificate by the given owner
    /// </summary>
    /// <param name="owner">The owner of the slices</param>
    /// <param name="registry"></param>
    /// <param name="certificateId"></param>
    /// <param name="quantity"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown when the owner does not have enough to reserve the requested amount</exception>
    /// <exception cref="TransientException">Thrown when the owner currently does not have enogth available, but will have later</exception>
    public async Task<IList<Slice>> ReserveQuantity(string owner, string registry, Guid certificateId, uint quantity)
    {
        var availableSlices = await GetOwnerAvailableSlices(registry, certificateId, owner);
        if (availableSlices.IsEmpty())
            throw new InvalidOperationException($"Owner has no available slices to reserve");

        if (availableSlices.Sum(slice => slice.Quantity) < quantity)
        {
            var toBeAvailable = await GetToBeAvailable(registry, certificateId, owner);
            if (toBeAvailable > quantity)
                throw new TransientException($"Owner has enough quantity, but it is not yet available to reserve");
            else
                throw new InvalidOperationException($"Owner has less to reserve than available");
        }

        var sumSlicesTaken = 0L;
        var takenSlices = availableSlices
            .OrderBy(slice => slice.Quantity)
            .TakeWhile(slice => { var needsMore = sumSlicesTaken < quantity; sumSlicesTaken += slice.Quantity; return needsMore; })
            .ToList();

        foreach (var slice in takenSlices)
        {
            await SetSliceState(slice.Id, SliceState.Reserved);
        }

        return takenSlices;
    }

    public Task InsertClaim(Claim newClaim)
    {
        return _connection.ExecuteAsync(@"INSERT INTO claims(id, production_slice_id, consumption_slice_id, state) VALUES (@id, @productionSliceId, @consumptionSliceId, @state)",
            new { newClaim.Id, newClaim.ProductionSliceId, newClaim.ConsumptionSliceId, newClaim.State });
    }

    public Task SetClaimState(Guid claimId, ClaimState state)
    {
        return _connection.ExecuteAsync("UPDATE claims SET state = @state WHERE id = @claimId", new { claimId, state });
    }

    public Task<Claim> GetClaim(Guid claimId)
    {
        return _connection.QuerySingleAsync<Claim>("SELECT * FROM claims WHERE id = @claimId", new { claimId });
    }
}
