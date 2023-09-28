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
    private readonly IDbConnection _connection;

    public CertificateRepository(IDbConnection connection) => this._connection = connection;

    public async Task InsertSlice(Slice newSlice)
    {
        var registryId = await _connection.QuerySingleAsync<Guid>(
            @"SELECT Id
              FROM Registries
              WHERE Name = @registry",
            new
            {
                newSlice.Registry
            });

        await _connection.ExecuteAsync(
            @"INSERT INTO Slices(Id, DepositEndpointId, DepositEndpointPosition, RegistryId, CertificateId, Quantity, RandomR, SliceState)
              VALUES (@id, @depositEndpointId, @depositEndpointPosition, @registryId, @certificateId, @quantity, @randomR, @sliceState)",
            new
            {
                newSlice.Id,
                newSlice.DepositEndpointId,
                newSlice.DepositEndpointPosition,
                registryId,
                newSlice.CertificateId,
                newSlice.Quantity,
                newSlice.RandomR,
                newSlice.SliceState
            });
    }

    public async Task InsertCertificate(Certificate certificate)
    {
        var registryId = await _connection.QuerySingleOrDefaultAsync<Guid?>(
            @"SELECT Id
              FROM Registries
              WHERE Name = @registry",
            new
            {
                certificate.Registry
            });

        if (!registryId.HasValue)
        {
            registryId = Guid.NewGuid();
            await _connection.ExecuteAsync(
                @"INSERT INTO Registries(Id, Name)
                  VALUES (@registryId, @registry)",
                new
                {
                    registryId,
                    certificate.Registry
                });
        }

        await _connection.ExecuteAsync(
            @"INSERT INTO Certificates(Id, RegistryId, StartDate, EndDate, GridArea, CertificateType)
              VALUES (@id, @registryId, @startDate, @endDate, @gridArea, @certificateType)",
            new
            {
                certificate.Id,
                registryId,
                startDate = certificate.StartDate.ToUtcTime(),
                endDate = certificate.EndDate.ToUtcTime(),
                certificate.GridArea,
                certificate.CertificateType
            });

        foreach (var atr in certificate.Attributes)
        {
            var atrId = Guid.NewGuid();
            await _connection.ExecuteAsync(
                @"INSERT INTO Attributes(Id, KeyAtr, ValueAtr, CertificateId, RegistryId)
                  VALUES (@atrId, @key, @value, @id, @registryId)",
                new
                {
                    atrId,
                    atr.Key,
                    atr.Value,
                    certificate.Id,
                    registryId
                });
        }
    }

    public async Task<Certificate?> GetCertificate(string registryName, Guid certificateId)
    {
        var certsDictionary = new Dictionary<Guid, Certificate>();
        await _connection.QueryAsync<Certificate?, CertificateAttribute, Certificate?>(
            @"SELECT c.Id, r.Name as Registry, c.StartDate, c.EndDate, c.GridArea, c.CertificateType, a.Id AS AttributeId, a.KeyAtr AS Key, a.ValueAtr as Value
              FROM Certificates c
              INNER JOIN Registries r
                ON c.RegistryId = r.Id
              LEFT JOIN Attributes a
                ON c.Id = a.CertificateId
                AND c.RegistryId = a.RegistryId
              WHERE c.Id = @certificateId
                AND r.Name = @registryName",
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
            },
            splitOn: "AttributeId",
            param: new
            {
                certificateId,
                registryName
            });

        return certsDictionary.Values.FirstOrDefault();
    }

    public async Task<IEnumerable<CertificateViewModel>> GetAllOwnedCertificates(string owner)
    {
        var certsDictionary = new Dictionary<Guid, CertificateViewModel>();

        await _connection.QueryAsync<CertificateViewModel, SliceViewModel, CertificateAttribute, CertificateViewModel>(
            @"SELECT c.Id, r.Name as Registry, c.StartDate, c. EndDate, c.GridArea, c.CertificateType, s.Id AS SliceId, s.Quantity as Quantity, a.Id AS AttributeId, a.KeyAtr AS Key, a.ValueAtr as Value
              FROM Wallets w
              INNER JOIN DepositEndpoints de
                ON w.Id = de.WalletId
              INNER JOIN Slices s
                ON de.Id = s.DepositEndpointId
              INNER JOIN Certificates c
                ON s.CertificateId = c.Id
              INNER JOIN Registries r
                ON c.RegistryId = r.Id
              LEFT JOIN Attributes a
                ON c.Id = a.CertificateId
                AND c.RegistryId = a.RegistryId
              WHERE w.Owner = @owner
                AND s.SliceState = @sliceState",
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
            param: new
            {
                owner,
                sliceState = (int)SliceState.Available
            });

        return certsDictionary.Values;
    }

    public Task<ReceivedSlice?> GetTop1ReceivedSlice()
    {
        return _connection.QueryFirstOrDefaultAsync<ReceivedSlice?>(
            @"SELECT *
              FROM ReceivedSlices LIMIT 1 FOR UPDATE");
    }

    public Task RemoveReceivedSlice(ReceivedSlice receivedSlice)
    {
        return _connection.ExecuteAsync(
            @"DELETE FROM ReceivedSlices
              WHERE Id = @id",
            new
            {
                receivedSlice.Id
            });
    }

    public Task<IEnumerable<Slice>> GetOwnersAvailableSlices(string registryName, Guid certificateId, string owner)
    {
        return _connection.QueryAsync<Slice>(
            @"SELECT s.*, r.Name as Registry
              FROM Certificates c
              INNER JOIN Slices s
                ON c.Id = s.CertificateId
              INNER JOIN Registries r
                ON s.RegistryId = r.Id
              INNER JOIN DepositEndpoints de
                ON s.DepositEndpointId = de.Id
              INNER JOIN Wallets w
                ON de.WalletId = w.Id
              WHERE r.Name = @registryName
                AND s.CertificateId = @certificateId
                AND w.owner = @owner
                AND s.SliceState = @sliceState",
            new
            {
                registryName,
                certificateId,
                owner,
                sliceState = (int)SliceState.Available
            });
    }

    public Task<long> GetRegisteringAndAvailableQuantity(string registryName, Guid certificateId, string owner)
    {
        return _connection.QuerySingleAsync<long>(
            @"SELECT SUM(s.quantity)
              FROM Certificates c
              INNER JOIN Slices s
                ON c.Id = s.CertificateId
              INNER JOIN Registries r
                ON s.RegistryId = r.Id
              INNER JOIN DepositEndpoints de
                ON s.DepositEndpointId = de.Id
              INNER JOIN Wallets w
                ON de.WalletId = w.Id
              WHERE r.Name = @registryName
                AND s.CertificateId = @certificateId
                AND w.owner = @owner
                AND (s.SliceState = @availableState OR s.SliceState = @registeringState)",
            new
            {
                registryName,
                certificateId,
                owner,
                availableState = (int)SliceState.Available,
                registeringState = (int)SliceState.Registering
            });
    }

    public Task<Slice> GetSlice(Guid sliceId)
    {
        return _connection.QuerySingleAsync<Slice>(
            @"SELECT s.*, r.Name as Registry
              FROM Slices s
              INNER JOIN Registries r
                ON s.RegistryId = r.Id
              WHERE s.Id = @sliceId",
            new
            {
                sliceId
            });
    }

    public Task SetSliceState(Guid sliceId, SliceState state)
    {
        return _connection.ExecuteAsync(
            @"UPDATE Slices
              SET SliceState = @state
              WHERE Id = @sliceId",
            new
            {
                sliceId,
                state
            });
    }

    /// <summary>
    /// Reserves the requested quantity of slices of the given certificate by the given owner
    /// </summary>
    /// <param name="owner">The owner of the slices</param>
    /// <param name="registryName"></param>
    /// <param name="certificateId"></param>
    /// <param name="reserveQuantity"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException">Thrown when the owner does not have enough to reserve the requested amount</exception>
    /// <exception cref="TransientException">Thrown when the owner currently does not have enogth available, but will have later</exception>
    public async Task<IList<Slice>> ReserveQuantity(string owner, string registryName, Guid certificateId, uint reserveQuantity)
    {
        var availableSlices = await GetOwnersAvailableSlices(registryName, certificateId, owner);
        if (availableSlices.IsEmpty())
            throw new InvalidOperationException($"Owner has no available slices to reserve");

        if (availableSlices.Sum(slice => slice.Quantity) < reserveQuantity)
        {
            var registeringAvailableQuantity = await GetRegisteringAndAvailableQuantity(registryName, certificateId, owner);
            if (registeringAvailableQuantity >= reserveQuantity)
                throw new TransientException($"Owner has enough quantity, but it is not yet available to reserve");
            else
                throw new InvalidOperationException($"Owner has less to reserve than available");
        }

        var sumSlicesTaken = 0L;
        var takenSlices = availableSlices
            .OrderBy(slice => slice.Quantity)
            .TakeWhile(slice => { var needsMore = sumSlicesTaken < reserveQuantity; sumSlicesTaken += slice.Quantity; return needsMore; })
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
