using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.Common.V1;
using ProjectOrigin.WalletSystem.Server.Activities.Exceptions;
using ProjectOrigin.WalletSystem.Server.Extensions;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.ViewModels;

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
                    certificate = cert;
                    certsDictionary.Add(cert.Id, cert);
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

    public async Task<CertificateViewModel?> QueryCertificate(string owner, string registry, Guid certificateId)
    {
        string sql = @"
            CREATE TEMPORARY TABLE certificates_work_table ON COMMIT DROP AS (
                SELECT
                    certificate_id,
                    registry_name,
                    certificate_type,
                    grid_area,
                    start_date,
                    end_date,
                    quantity,
                    wallet_id,
                    updated_at
                FROM
                    certificates_query_model
                WHERE
                    owner = @owner
                    AND registry_name = @registry
                    AND certificate_id = @certificateId
            );
            SELECT * FROM certificates_work_table;
            SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
            FROM attributes_view attributes
            WHERE (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT registry_name, certificate_id FROM certificates_work_table))
                OR (wallet_id, registry_name, certificate_id) IN (SELECT wallet_id, registry_name, certificate_id FROM certificates_work_table)

            ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, new { owner, registry, certificateId }))
        {
            var certificates = gridReader.Read<CertificateViewModel>();
            var attributes = gridReader.Read<AttributeViewModel>();

            var certificate = certificates.FirstOrDefault();
            certificate?.Attributes.AddRange(attributes
                .Where(attr => attr.RegistryName == certificate.RegistryName
                               && attr.CertificateId == certificate.CertificateId));

            return certificate;
        }
    }


    public async Task<PageResultCursor<CertificateViewModel>> QueryCertificates(QueryCertificatesFilterCursor filter)
    {
        string sql = @"
            CREATE TEMPORARY TABLE certificates_work_table ON COMMIT DROP AS (
                SELECT
                    certificate_id,
                    registry_name,
                    certificate_type,
                    grid_area,
                    start_date,
                    end_date,
                    quantity,
                    wallet_id,
                    updated_at
                FROM
                    certificates_query_model
                WHERE
                    owner = @owner
                    AND (@start IS NULL OR start_date >= @start)
                    AND (@end IS NULL OR end_date <= @end)
                    AND (@type IS NULL OR certificate_type = @type)
            );
            SELECT count(*) FROM certificates_work_table;
            SELECT * FROM certificates_work_table WHERE (@UpdatedSince IS NULL OR updated_at > @UpdatedSince) LIMIT @limit;
            SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
            FROM attributes_view attributes
            WHERE (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT registry_name, certificate_id FROM certificates_work_table))
                OR (wallet_id, registry_name, certificate_id) IN (SELECT wallet_id, registry_name, certificate_id FROM certificates_work_table)

            ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, filter))
        {
            var totalCouunt = gridReader.ReadSingle<int>();
            var certificates = gridReader.Read<CertificateViewModel>();
            var attributes = gridReader.Read<AttributeViewModel>();

            foreach (var certificate in certificates)
            {
                certificate.Attributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == certificate.RegistryName
                                   && attr.CertificateId == certificate.CertificateId));
            }

            return new PageResultCursor<CertificateViewModel>()
            {
                Items = certificates,
                TotalCount = totalCouunt,
                Count = certificates.Count(),
                updatedAt = filter.UpdatedSince?.ToUnixTimeSeconds(),
                Limit = filter.Limit
            };
        }
    }

    public async Task<PageResult<CertificateViewModel>> QueryAvailableCertificates(QueryCertificatesFilter filter)
    {
        string sql = @"
            CREATE TEMPORARY TABLE certificates_work_table ON COMMIT DROP AS (
                SELECT
                    certificate_id,
                    registry_name,
                    certificate_type,
                    grid_area,
                    start_date,
                    end_date,
                    quantity,
                    wallet_id,
                    updated_at
                FROM
                    certificates_query_model
                WHERE
                    owner = @owner
                    AND (@start IS NULL OR start_date >= @start)
                    AND (@end IS NULL OR end_date <= @end)
                    AND quantity != 0
                    AND (@type IS NULL OR certificate_type = @type)
            );
            SELECT count(*) FROM certificates_work_table;
            SELECT * FROM certificates_work_table LIMIT @limit OFFSET @skip;
            SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
            FROM attributes_view attributes
            WHERE (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT registry_name, certificate_id FROM certificates_work_table))
                OR (wallet_id, registry_name, certificate_id) IN (SELECT wallet_id, registry_name, certificate_id FROM certificates_work_table)
            ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, filter))
        {
            var totalCount = gridReader.ReadSingle<int>();
            var certificates = gridReader.Read<CertificateViewModel>();
            var attributes = gridReader.Read<AttributeViewModel>();

            foreach (var certificate in certificates)
            {
                certificate.Attributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == certificate.RegistryName
                                   && attr.CertificateId == certificate.CertificateId));
            }

            return new PageResult<CertificateViewModel>()
            {
                Items = certificates,
                TotalCount = totalCount,
                Count = certificates.Count(),
                Offset = filter.Skip,
                Limit = filter.Limit
            };
        }
    }

    public async Task<PageResult<AggregatedCertificatesViewModel>> QueryAggregatedAvailableCertificates(
        QueryAggregatedCertificatesFilter filter)
    {
        string sql = @"
            CREATE TEMPORARY TABLE certificates_work_table ON COMMIT DROP AS (
                SELECT *
                FROM (
                    SELECT
                        certificate_type as type,
                        min(start_date) as start,
                        max(end_date) as end,
                        sum(quantity) as quantity
                    FROM
                        certificates_query_model
                    WHERE
                        owner = @owner
                        AND (@start IS NULL OR start_date >= @start)
                        AND (@end IS NULL OR end_date <= @end)
                        AND quantity != 0
                        AND (@type IS NULL OR certificate_type = @type)
                    GROUP BY
                        CASE
                            WHEN @timeAggregate = 'total' THEN NULL
                            WHEN @timeAggregate = 'quarterhour' THEN date_trunc('hour', start_date AT TIME ZONE @timeZone) + INTERVAL '15 min' * ROUND(EXTRACT(MINUTE FROM start_date AT TIME ZONE @timeZone) / 15.0)
                            WHEN @timeAggregate = 'actual' THEN start_date AT TIME ZONE @timeZone
                            ELSE date_trunc(@timeAggregate, start_date AT TIME ZONE @timeZone)
                        END,
                        type
                    ) as aggregates
                ORDER BY
                    start,
                    type
            );
            SELECT count(*) FROM certificates_work_table;
            SELECT * FROM certificates_work_table LIMIT @limit OFFSET @skip;
            ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, new
        {
            filter.Owner,
            filter.Start,
            filter.End,
            filter.Type,
            filter.Skip,
            filter.Limit,
            timeAggregate = filter.TimeAggregate.ToString().ToLower(),
            filter.TimeZone
        }))
        {
            var totalCount = gridReader.ReadSingle<int>();
            var certificates = gridReader.Read<AggregatedCertificatesViewModel>();

            return new PageResult<AggregatedCertificatesViewModel>()
            {
                Items = certificates,
                TotalCount = totalCount,
                Count = certificates.Count(),
                Offset = filter.Skip,
                Limit = filter.Limit
            };
        }
    }

    public Task<IEnumerable<WalletSlice>> GetOwnersAvailableSlices(string registryName, Guid certificateId,
        string owner)
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
    public async Task<IList<WalletSlice>> ReserveQuantity(string owner, string registryName, Guid certificateId,
        uint reserveQuantity)
    {
        var availableSlices = await GetOwnersAvailableSlices(registryName, certificateId, owner);
        if (availableSlices.IsEmpty())
            throw new InvalidOperationException($"Owner has no available slices to reserve");

        if (availableSlices.Sum(slice => slice.Quantity) < reserveQuantity)
        {
            var registeringAvailableQuantity =
                await GetRegisteringAndAvailableQuantity(registryName, certificateId, owner);
            if (registeringAvailableQuantity >= reserveQuantity)
                throw new TransientException($"Owner has enough quantity, but it is not yet available to reserve");
            else
                throw new InvalidOperationException($"Owner has less to reserve than available");
        }

        var sumSlicesTaken = 0L;
        var takenSlices = availableSlices
            .OrderBy(slice => slice.Quantity)
            .TakeWhile(slice =>
            {
                var needsMore = sumSlicesTaken < reserveQuantity;
                sumSlicesTaken += slice.Quantity;
                return needsMore;
            })
            .ToList();

        foreach (var slice in takenSlices)
        {
            await SetWalletSliceState(slice.Id, WalletSliceState.Reserved);
        }

        return takenSlices;
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

    public async Task<IEnumerable<WalletAttribute>> GetWalletAttributes(Guid walletId, Guid certificateId,
        string registryName, IEnumerable<string> keys)
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
}
