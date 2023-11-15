using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using Claim = ProjectOrigin.WalletSystem.Server.Models.Claim;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class CertificateRepository : ICertificateRepository
{
    private readonly IDbConnection _connection;

    public CertificateRepository(IDbConnection connection) => this._connection = connection;

    public async Task InsertWalletSlice(WalletSlice newSlice)
    {
        await _connection.ExecuteAsync(
            @"INSERT INTO wallet_slices(id, certificate_id, registry_name, wallet_endpoint_id, wallet_endpoint_position, state, quantity, random_r)
              VALUES (@id, @certificateId, @registryName, @walletEndpointId, @walletEndpointPosition, @state, @quantity, @randomR)",
            newSlice);
    }

    public async Task InsertTransferredSlice(TransferredSlice newSlice)
    {
        await _connection.ExecuteAsync(
            @"INSERT INTO transferred_slices(id, certificate_id, registry_name, external_endpoint_id, external_endpoint_position, state, quantity, random_r)
              VALUES (@id, @certificateId, @registryName, @externalEndpointId, @externalEndpointPosition, @state, @quantity, @randomR)",
            newSlice);
    }

    public async Task InsertCertificate(Certificate certificate)
    {
        await _connection.ExecuteAsync(
            @"INSERT INTO certificates(id, registry_name, start_date, end_date, grid_area, certificate_type)
              VALUES (@id, @registryName, @startDate, @endDate, @gridArea, @certificateType)",
            new
            {
                certificate.Id,
                certificate.RegistryName,
                startDate = certificate.StartDate.ToUtcTime(),
                endDate = certificate.EndDate.ToUtcTime(),
                certificate.GridArea,
                certificate.CertificateType
            });

        foreach (var atr in certificate.Attributes)
        {
            await _connection.ExecuteAsync(
                @"INSERT INTO attributes(id, attribute_key, attribute_value, attribute_type, certificate_id, registry_name)
                  VALUES (@id, @key, @value, @type, @certificateId, @registryName)",
                new
                {
                    id = Guid.NewGuid(),
                    atr.Key,
                    atr.Value,
                    atr.Type,
                    certificateId = certificate.Id,
                    certificate.RegistryName
                });
        }
    }

    public async Task<Certificate?> GetCertificate(string registryName, Guid certificateId)
    {
        var certsDictionary = new Dictionary<Guid, Certificate>();
        await _connection.QueryAsync<Certificate?, CertificateAttribute, Certificate?>(
            @"SELECT c.*, a.attribute_key as key, a.attribute_value as value, a.attribute_type as type
              FROM certificates c
              LEFT JOIN Attributes a
                ON c.id = a.certificate_id
                AND c.registry_name = a.registry_name
              WHERE c.id = @certificateId
                AND c.registry_name = @registryName",
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
            splitOn: "key",
            param: new
            {
                certificateId,
                registryName
            });

        return certsDictionary.Values.FirstOrDefault();
    }

    public async Task<IEnumerable<CertificateViewModel>> GetAllOwnedCertificates(string owner, CertificatesFilter filter)
    {
        var certsDictionary = new Dictionary<Guid, CertificateViewModel>();

        await _connection.QueryAsync<CertificateViewModel, SliceViewModel, CertificateAttribute, CertificateViewModel>(
            @"SELECT c.*, s.Id AS slice_id, s.quantity, a.attribute_key as key, a.attribute_value as value, a.attribute_type as type
               FROM Wallets w
               INNER JOIN wallet_endpoints re
                ON w.id = re.wallet_id
              INNER JOIN wallet_slices s
                ON re.Id = s.wallet_endpoint_id
              INNER JOIN certificates c
                ON s.certificate_id = c.id
              LEFT JOIN attributes_view a
                ON c.id = a.certificate_id
                 AND c.registry_name = a.registry_name
                 AND (a.wallet_id = w.id OR a.wallet_id IS NULL)
               WHERE w.owner = @owner
                 AND s.state = @sliceState
                 AND (@start IS NULL OR c.start_date >= @start)
                 AND (@end IS NULL OR c.end_date <= @end)
                 AND (@type IS NULL OR c.certificate_type = @type)",
            (cert, slice, atr) =>
            {
                if (!certsDictionary.TryGetValue(cert.Id, out var certificate))
                {
                    certsDictionary.Add(cert.Id, certificate = cert);
                }
                if (slice != null && !certificate.Slices.Contains(slice))
                    certificate.Slices.Add(slice);
                if (atr != null && atr.Key != null && atr.Value != null && !certificate.Attributes.Contains(atr))
                    certificate.Attributes.Add(atr);
                return certificate;
            },
            splitOn: "slice_id, key",
            param: new
            {
                owner,
                sliceState = (int)WalletSliceState.Available,
                start = filter.Start,
                end = filter.End,
                type = filter.Type
            });

        return certsDictionary.Values;
    }

    public Task<IEnumerable<WalletSlice>> GetOwnersAvailableSlices(string registryName, Guid certificateId, string owner)
    {
        return _connection.QueryAsync<WalletSlice>(
            @"SELECT s.*
              FROM certificates c
              INNER JOIN wallet_slices s
                ON c.id = s.certificate_id
              INNER JOIN wallet_endpoints re
                ON s.wallet_endpoint_id = re.id
              INNER JOIN wallets w
                ON re.wallet_id = w.id
              WHERE s.registry_name = @registryName
                AND s.certificate_id = @certificateId
                AND w.owner = @owner
                AND s.state = @state",
            new
            {
                registryName,
                certificateId,
                owner,
                state = (int)WalletSliceState.Available
            });
    }

    public Task<long> GetRegisteringAndAvailableQuantity(string registryName, Guid certificateId, string owner)
    {
        return _connection.QuerySingleAsync<long>(
            @"SELECT SUM(s.quantity)
              FROM certificates c
              INNER JOIN wallet_slices s
                ON c.id = s.certificate_id
              INNER JOIN wallet_endpoints re
                ON s.wallet_endpoint_id = re.Id
              INNER JOIN wallets w
                ON re.wallet_id = w.id
              WHERE s.registry_name = @registryName
                AND s.certificate_id = @certificateId
                AND w.owner = @owner
                AND (s.state = @availableState OR s.state = @registeringState)",
            new
            {
                registryName,
                certificateId,
                owner,
                availableState = (int)WalletSliceState.Available,
                registeringState = (int)WalletSliceState.Registering
            });
    }

    public Task<WalletSlice> GetWalletSlice(Guid sliceId)
    {
        return _connection.QuerySingleAsync<WalletSlice>(
            @"SELECT s.*
              FROM wallet_slices s
              WHERE s.id = @sliceId",
            new
            {
                sliceId
            });
    }

    public Task<TransferredSlice> GetTransferredSlice(Guid sliceId)
    {
        return _connection.QuerySingleAsync<TransferredSlice>(
            @"SELECT s.*
              FROM transferred_slices s
              WHERE s.id = @sliceId",
            new
            {
                sliceId
            });
    }

    public async Task SetWalletSliceState(Guid sliceId, WalletSliceState state)
    {
        var rowsChanged = await _connection.ExecuteAsync(
            @"UPDATE wallet_slices
              SET state = @state
              WHERE id = @sliceId",
            new
            {
                sliceId,
                state
            });

        if (rowsChanged != 1)
            throw new InvalidOperationException($"Slice with id {sliceId} could not be found");
    }

    public async Task SetTransferredSliceState(Guid sliceId, TransferredSliceState state)
    {
        var rowsChanged = await _connection.ExecuteAsync(
            @"UPDATE transferred_slices
              SET state = @state
              WHERE id = @sliceId",
            new
            {
                sliceId,
                state
            });

        if (rowsChanged != 1)
            throw new InvalidOperationException($"Slice with id {sliceId} could not be found");
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
    public async Task<IList<WalletSlice>> ReserveQuantity(string owner, string registryName, Guid certificateId, uint reserveQuantity)
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
            await SetWalletSliceState(slice.Id, WalletSliceState.Reserved);
        }

        return takenSlices;
    }

    public Task InsertClaim(Claim newClaim)
    {
        return _connection.ExecuteAsync(
            @"INSERT INTO claims(id, production_slice_id, consumption_slice_id, state)
              VALUES (@id, @productionSliceId, @consumptionSliceId, @state)",
            new
            {
                newClaim.Id,
                newClaim.ProductionSliceId,
                newClaim.ConsumptionSliceId,
                newClaim.State
            });
    }

    public Task SetClaimState(Guid claimId, ClaimState state)
    {
        return _connection.ExecuteAsync(
            @"UPDATE claims
              SET state = @state
              WHERE id = @claimId",
            new
            {
                claimId,
                state
            });
    }

    public Task<Claim> GetClaim(Guid claimId)
    {
        return _connection.QuerySingleAsync<Claim>(
            @"SELECT *
              FROM claims
              WHERE id = @claimId",
            new
            {
                claimId
            });
    }

    public async Task<IEnumerable<ClaimViewModel>> GetClaims(string owner, ClaimFilter claimFilter)
    {
        string sql = @"
        CREATE TEMPORARY TABLE claims_work_table ON COMMIT DROP AS (
            SELECT
                claims.Id,
                slice_cons.quantity AS Quantity,
                wallet_cons.id as WalletId,

                slice_prod.registry_name AS ProductionRegistryName,
                slice_prod.certificate_id AS ProductionCertificateId,
                cert_prod.start_date AS ProductionStart,
                cert_prod.end_date AS ProductionEnd,
                cert_prod.grid_area AS ProductionGridArea,

                slice_cons.registry_name AS ConsumptionRegistryName,
                slice_cons.certificate_id AS ConsumptionCertificateId,
                cert_cons.start_date AS ConsumptionStart,
                cert_cons.end_date AS ConsumptionEnd,
                cert_cons.grid_area AS ConsumptionGridArea

            FROM claims

            INNER JOIN wallet_slices slice_prod
                ON claims.production_slice_id = slice_prod.id
            INNER JOIN certificates cert_prod
                ON slice_prod.certificate_id = cert_prod.id
                AND slice_prod.registry_name = cert_prod.registry_name

            INNER JOIN wallet_slices slice_cons
                ON claims.consumption_slice_id = slice_cons.id
            INNER JOIN certificates cert_cons
                ON slice_cons.certificate_id = cert_cons.id
                AND slice_cons.registry_name = cert_cons.registry_name

            INNER JOIN wallet_endpoints dep_cons
                ON slice_cons.wallet_endpoint_id = dep_cons.id
            INNER JOIN wallets wallet_cons
                ON dep_cons.wallet_id = wallet_cons.id

            WHERE
                claims.state = @state
                AND (@start IS NULL OR cert_prod.start_date >= @start)
                AND (@end IS NULL OR cert_prod.end_date <= @end)
                AND (@start IS NULL OR cert_cons.start_date >= @start)
                AND (@end IS NULL OR cert_cons.end_date <= @end)
                AND wallet_cons.owner = @owner
        );
        SELECT * FROM claims_work_table;
        SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
        FROM attributes_view attributes
        WHERE ((registry_name, certificate_id) IN (SELECT ConsumptionRegistryName, ConsumptionCertificateId FROM claims_work_table) AND wallet_id IS NULL)
            OR ((registry_name, certificate_id) IN (SELECT ProductionRegistryName, ProductionCertificateId FROM claims_work_table) AND wallet_id IS NULL)
            OR (registry_name, certificate_id, wallet_id) IN (SELECT ConsumptionRegistryName, ConsumptionCertificateId, WalletId FROM claims_work_table)
            OR (registry_name, certificate_id, wallet_id) IN (SELECT ProductionRegistryName, ProductionCertificateId, WalletId FROM claims_work_table);
        ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, new
        {
            owner,
            state = (int)ClaimState.Claimed,
            start = claimFilter.Start,
            end = claimFilter.End,
        }))
        {
            var claims = gridReader.Read<ClaimViewModel>();
            var attributes = gridReader.Read<ExtendedAttribute>();

            foreach (var claim in claims)
            {
                claim.ProductionAttributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == claim.ProductionRegistryName
                            && attr.CertificateId == claim.ProductionCertificateId));

                claim.ConsumptionAttributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == claim.ConsumptionRegistryName
                            && attr.CertificateId == claim.ConsumptionCertificateId));
            }

            return claims;
        }
    }

    public async Task InsertWalletAttribute(Guid walletId, WalletAttribute walletAttribute)
    {
        await _connection.ExecuteAsync(
               @"INSERT INTO wallet_attributes(id, wallet_id, certificate_id, registry_name, attribute_key, attribute_value, salt)
                  VALUES (@id, @walletId, @certificateId, @registryName, @attributeKey, @attributeValue, @salt)",
               new
               {
                   id = Guid.NewGuid(),
                   walletId,
                   walletAttribute.CertificateId,
                   walletAttribute.RegistryName,
                   attributeKey = walletAttribute.Key,
                   attributeValue = walletAttribute.Value,
                   walletAttribute.Salt
               });
    }

    public async Task<IEnumerable<WalletAttribute>> GetWalletAttributes(Guid walletId, Guid certificateId, string registryName, IEnumerable<string> keys)
    {
        return (await _connection.QueryAsync<WalletAttribute>(
            @"SELECT wallet_id, certificate_id, registry_name, attribute_key as key, attribute_value as value, salt
              FROM wallet_attributes
              WHERE wallet_id = @walletId
                AND certificate_id = @certificateId
                AND registry_name = @registryName
                AND attribute_key = ANY(@keys)",
            new
            {
                walletId,
                certificateId,
                registryName,
                keys = keys.ToArray(),
            })).AsList();
    }

    private sealed record ExtendedAttribute : CertificateAttribute
    {
        public required string RegistryName { get; init; }
        public required Guid CertificateId { get; init; }
    }
}
