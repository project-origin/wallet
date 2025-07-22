using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.Vault.Exceptions;
using ProjectOrigin.Vault.Extensions;
using ProjectOrigin.Vault.Models;
using ProjectOrigin.Vault.ViewModels;
using Serilog;
using Serilog.Formatting.Json;

namespace ProjectOrigin.Vault.Repositories;

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
            @"INSERT INTO certificates(id, registry_name, start_date, end_date, grid_area, certificate_type, withdrawn)
              VALUES (@id, @registryName, @startDate, @endDate, @gridArea, @certificateType, @withdrawn)",
            new
            {
                certificate.Id,
                certificate.RegistryName,
                startDate = certificate.StartDate.ToUtcTime(),
                endDate = certificate.EndDate.ToUtcTime(),
                certificate.GridArea,
                certificate.CertificateType,
                certificate.Withdrawn
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
                    updated_at,
                    withdrawn
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
            WHERE (registry_name, certificate_id) IN (SELECT registry_name, certificate_id FROM certificates_work_table)
			  AND (wallet_id IS NULL OR wallet_id in (SELECT DISTINCT(wallet_id) FROM certificates_work_table))

            ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, new { owner, registry, certificateId }))
        {
            var certificates = await gridReader.ReadAsync<CertificateViewModel>();
            var attributes = await gridReader.ReadAsync<AttributeViewModel>();

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
                WITH
            available_slices AS (
                SELECT wallet_endpoint_id, certificate_id, quantity, updated_at
                FROM wallet_slices
                WHERE state = 1 AND quantity != 0
            ),
            non_withdrawn_certificates AS (
                SELECT
                    id,
                    registry_name,
                    certificate_type,
                    grid_area,
                    start_date,
                    end_date,
                    withdrawn
                FROM certificates
                WHERE NOT withdrawn
            )
            SELECT * FROM (
                SELECT
                    c.id as certificate_id,
                    c.registry_name,
                    c.certificate_type,
                    c.grid_area,
                    c.start_date,
                    c.end_date,
                    w.id as wallet_id,
                    w.owner,
                    SUM(ws.quantity) as quantity,
                    MAX(ws.updated_at) as updated_at,
                    c.withdrawn
                FROM wallets w
                INNER JOIN wallet_endpoints we ON w.id = we.wallet_id
                INNER JOIN available_slices ws ON we.id = ws.wallet_endpoint_id
                INNER JOIN non_withdrawn_certificates c ON ws.certificate_id = c.id
                GROUP BY
                    c.id,
                    c.registry_name,
                    c.certificate_type,
                    c.grid_area,
                    c.start_date,
                    c.end_date,
                    w.id,
                    w.owner,
                    c.withdrawn
                ORDER BY updated_at ASC,
                        c.start_date ASC,
                        c.id ASC
            ) AS certs_aggregated
                WHERE
                    owner = @owner
                    AND withdrawn = false
                    AND (@start IS NULL OR start_date >= @start)
                    AND (@end IS NULL OR end_date <= @end)
                    AND (@type IS NULL OR certificate_type = @type)
                    AND quantity != 0
            );
            SELECT count(*) FROM certificates_work_table;

            CREATE TEMPORARY TABLE certificates_work_table_limit ON COMMIT DROP AS (
            SELECT * FROM certificates_work_table WHERE (@UpdatedSince IS NULL OR updated_at > @UpdatedSince) LIMIT @limit);
            SELECT * FROM certificates_work_table_limit;

            SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
            FROM attributes_view attributes
            WHERE (registry_name, certificate_id) IN (SELECT registry_name, certificate_id FROM certificates_work_table_limit)
			  AND (wallet_id IS NULL OR wallet_id in (SELECT DISTINCT(wallet_id) FROM certificates_work_table_limit));
            ";

        await using var gridReader = await _connection.QueryMultipleAsync(sql, filter);

        var totalCount = await gridReader.ReadSingleAsync<int>();
        var certificates = (await gridReader.ReadAsync<CertificateViewModel>()).ToArray();
        var attributes = await gridReader.ReadAsync<AttributeViewModel>();

        var attributeMap = attributes
            .GroupBy(attr => (attr.RegistryName, attr.CertificateId))
            .ToDictionary(group => group.Key, group => group.ToList());

        foreach (var certificate in certificates)
        {
            if (attributeMap.TryGetValue((certificate.RegistryName, certificate.CertificateId),
                    out var certificateAttributes))
            {
                certificate.Attributes.AddRange(certificateAttributes);
            }
        }

        return new PageResultCursor<CertificateViewModel>()
        {
            Items = certificates,
            TotalCount = totalCount,
            Count = certificates.Length,
            updatedAt = filter.UpdatedSince?.ToUnixTimeSeconds(),
            Limit = filter.Limit
        };
    }

    public async Task<PageResult<CertificateViewModel>> QueryAvailableCertificates(QueryCertificatesFilter filter)
    {
        string sql = @"
            CREATE TEMPORARY TABLE certificates_work_table ON COMMIT DROP AS
            WITH
            available_slices AS (
                SELECT wallet_endpoint_id, certificate_id, quantity, updated_at
                FROM wallet_slices
                WHERE state = 1 AND quantity != 0
            ),
            non_withdrawn_certificates AS (
                SELECT
                    id,
                    registry_name,
                    certificate_type,
                    grid_area,
                    start_date,
                    end_date,
                    withdrawn
                FROM certificates
                WHERE NOT withdrawn
            )
            SELECT * FROM (
                SELECT
                    c.id as certificate_id,
                    c.registry_name,
                    c.certificate_type,
                    c.grid_area,
                    c.start_date,
                    c.end_date,
                    w.id as wallet_id,
                    w.owner,
                    SUM(ws.quantity) as quantity,
                    MAX(ws.updated_at) as updated_at,
                    c.withdrawn
                FROM wallets w
                INNER JOIN wallet_endpoints we ON w.id = we.wallet_id
                INNER JOIN available_slices ws ON we.id = ws.wallet_endpoint_id
                INNER JOIN non_withdrawn_certificates c ON ws.certificate_id = c.id
                GROUP BY
                    c.id,
                    c.registry_name,
                    c.certificate_type,
                    c.grid_area,
                    c.start_date,
                    c.end_date,
                    w.id,
                    w.owner,
                    c.withdrawn
            ) AS certs_aggregated
            WHERE
                owner = @owner
                AND withdrawn = false
                AND (@start IS NULL OR start_date >= @start)
                AND (@end IS NULL OR end_date <= @end)
                AND quantity != 0
                AND (@type IS NULL OR certificate_type = @type)
            ORDER BY
                updated_at ASC,
                start_date ASC,
                certificate_id ASC;

            SELECT count(*) FROM certificates_work_table;

            CREATE TEMPORARY TABLE certificates_work_table_limit ON COMMIT DROP AS (
            SELECT *
            FROM certificates_work_table
            ORDER BY
                CASE WHEN @SortBy = 'End' AND @Sort = 'ASC' THEN end_date END ASC,
                CASE WHEN @SortBy = 'End' AND @Sort = 'DESC' THEN end_date END DESC,
                CASE WHEN @SortBy = 'Quantity' AND @Sort = 'ASC' THEN quantity END ASC,
                CASE WHEN @SortBy = 'Quantity' AND @Sort = 'DESC' THEN quantity END DESC,
                CASE WHEN @SortBy = 'Type' AND @Sort = 'ASC' THEN certificate_type END ASC,
                CASE WHEN @SortBy = 'Type' AND @Sort = 'DESC' THEN certificate_type END DESC
            LIMIT @limit OFFSET @skip);

            SELECT * FROM certificates_work_table_limit;

            SELECT
                a.registry_name,
                a.certificate_id,
                a.attribute_key AS key,
                a.attribute_value AS value,
                a.attribute_type AS type
            FROM
                attributes_view a
                JOIN certificates_work_table_limit cwt
                  ON a.registry_name = cwt.registry_name
                 AND a.certificate_id = cwt.certificate_id
            WHERE
                (a.wallet_id IS NULL OR a.wallet_id = cwt.wallet_id);
            ";

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            await using var gridReader = await _connection.QueryMultipleAsync(sql, filter, commandTimeout: 60);

            var totalCount = await gridReader.ReadSingleAsync<int>();
            var certificates = (await gridReader.ReadAsync<CertificateViewModel>()).ToArray();
            var attributes = await gridReader.ReadAsync<AttributeViewModel>();

            var attributeMap = attributes
                .GroupBy(attr => (attr.RegistryName, attr.CertificateId))
                .ToDictionary(group => group.Key, group => group.ToList());

            foreach (var certificate in certificates)
            {
                if (attributeMap.TryGetValue((certificate.RegistryName, certificate.CertificateId),
                        out var certificateAttributes))
                {
                    certificate.Attributes.AddRange(certificateAttributes);
                }
            }

            var result = new PageResult<CertificateViewModel>()
            {
                Items = certificates,
                TotalCount = totalCount,
                Count = certificates.Count(),
                Offset = filter.Skip,
                Limit = filter.Limit
            };

            new LoggerConfiguration()
                .WriteTo.Console(new JsonFormatter())
                .CreateLogger()
                .Information("Successfully completed QueryAvailableCertificates in {ElapsedMilliseconds} ms, returning {Count} of {TotalCount} certificates, with filters: start: {Start}, end: {End}, type: {Type}, skip: {Skip}, limit: {Limit}",
                    stopwatch.ElapsedMilliseconds, certificates.Length, totalCount, filter.Start, filter.End, filter.Type, filter.Skip, filter.Limit);

            return result;
        }
        catch (Exception ex)
        {
            new LoggerConfiguration()
                .WriteTo.Console(new JsonFormatter())
                .CreateLogger()
                .Error(ex,
                    "Error in QueryAvailableCertificates executed in {ElapsedMilliseconds} ms, with filters: start: {Start}, end: {End}, type: {Type}, skip: {Skip}, limit: {Limit}",
                    stopwatch.ElapsedMilliseconds, filter.Start, filter.End, filter.Type, filter.Skip, filter.Limit);
            throw;
        }
        finally
        {
            stopwatch.Stop();
        }
    }

    public async Task<PageResult<AggregatedCertificatesViewModel>> QueryAggregatedAvailableCertificates(
        QueryAggregatedCertificatesFilter filter)
    {
        string sql = @"
            CREATE TEMPORARY TABLE certificates_work_table ON COMMIT DROP AS (
            WITH 
            available_slices AS (
                SELECT wallet_endpoint_id, certificate_id, quantity, updated_at
                FROM wallet_slices
                WHERE state = 1 AND quantity != 0
            ),
	        non_withdrawn_certificates AS (
		        SELECT
			        id,
			        registry_name,
			        certificate_type,
			        grid_area,
			        start_date,
			        end_date,
			        withdrawn
		        FROM certificates
		        WHERE NOT withdrawn
	        ),
	        certificates_raw AS (
	            SELECT
	                c.id as certificate_id,
	                c.registry_name,
	                c.certificate_type,
	                c.grid_area,
	                c.start_date,
	                c.end_date,
	                w.id as wallet_id,
	                w.owner,
	                SUM(ws.quantity) as quantity,
	                MAX(ws.updated_at) as updated_at,
	                c.withdrawn
	            FROM wallets w
	            INNER JOIN wallet_endpoints we ON w.id = we.wallet_id
	            INNER JOIN available_slices ws ON we.id = ws.wallet_endpoint_id
	            INNER JOIN non_withdrawn_certificates c ON ws.certificate_id = c.id
	            GROUP BY
	                c.id,
	                c.registry_name,
	                c.certificate_type,
	                c.grid_area,
	                c.start_date,
	                c.end_date,
	                w.id,
	                w.owner,
	                c.withdrawn
	        )
            SELECT *
            FROM (
                SELECT
                    certificate_type as type,
                    min(start_date) as start,
                    max(end_date) as end,
                    sum(quantity) as quantity
                FROM
                    certificates_raw
                WHERE
                    owner = @owner
                    AND withdrawn = false
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
            var totalCount = await gridReader.ReadSingleAsync<int>();
            var certificates = await gridReader.ReadAsync<AggregatedCertificatesViewModel>();

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
            @"WITH slice_data AS (
                    SELECT *
                    FROM wallet_slices ws
                    WHERE ws.certificate_id = @certificateId
                      AND ws.registry_name = @registryName
                      AND (ws.state = @state)
                    FOR UPDATE OF ws
                  )
              SELECT s.*
              FROM certificates c
              INNER JOIN slice_data s
                ON c.id = s.certificate_id
              INNER JOIN wallet_endpoints re
                ON s.wallet_endpoint_id = re.id
              INNER JOIN wallets w
                ON re.wallet_id = w.id
              WHERE s.registry_name = @registryName
                AND s.certificate_id = @certificateId
                AND w.owner = @owner
                AND s.state = @state
                AND c.withdrawn = false",
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
            @"WITH slice_data AS (
                    SELECT *
                    FROM wallet_slices ws
                    WHERE ws.certificate_id = @certificateId
                      AND ws.registry_name = @registryName
                      AND (ws.state = @availableState OR ws.state = @registeringState)
                    FOR UPDATE OF ws
                  )
                  SELECT CASE WHEN SUM(s.quantity) IS NULL THEN 0 ELSE SUM(s.quantity) END AS total_quantity
                  FROM certificates c
                  INNER JOIN slice_data s on c.id = s.certificate_id
                  INNER JOIN wallet_endpoints re ON s.wallet_endpoint_id = re.Id
                  INNER JOIN wallets w ON re.wallet_id = w.id
                  WHERE s.registry_name = @registryName
	                AND s.certificate_id = @certificateId
	                AND w.owner = @owner
                    AND c.withdrawn = false
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

    public async Task WithdrawCertificate(string registry, Guid certificateId)
    {
        var rowsChanged = await _connection.ExecuteAsync(
            @"UPDATE certificates
              SET withdrawn = true
              WHERE registry_name = @registry
                AND id = @certificateId",
            new
            {
                registry,
                certificateId
            });

        if (rowsChanged != 1)
            throw new InvalidOperationException(
                $"Rows changed: {rowsChanged}. Certificate with registry {registry} and certificateId {certificateId} could not be found");
    }

    public async Task<IEnumerable<WalletSlice>> GetClaimedSlicesOfCertificate(string registry, Guid certificateId)
    {
        return await _connection.QueryAsync<WalletSlice>(
            @"SELECT *
                  FROM wallet_slices
                  WHERE registry_name = @registry
                  AND certificate_id = @certificateId
                  AND state = @state",
            new
            {
                registry,
                certificateId,
                state = WalletSliceState.Claimed
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
    /// <exception cref="QuantityNotYetAvailableToReserveException">Thrown when the owner currently does not have enogth available, but will have later</exception>
    public async Task<IList<WalletSlice>> ReserveQuantity(string owner, string registryName, Guid certificateId,
        uint reserveQuantity)
    {
        var slices = await GetOwnersAvailableSlices(registryName, certificateId, owner);
        var willBeAvailable = await GetRegisteringAndAvailableQuantity(registryName, certificateId, owner);

        if (slices.Sum(x => x.Quantity) >= reserveQuantity)
        {
            var sumSlicesTaken = 0L;
            var takenSlices = slices.Where(s => s.State == WalletSliceState.Available)
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
                var rowsChanged = await _connection.ExecuteAsync(
                    @"UPDATE wallet_slices
                          SET state = @state
                          WHERE id = @sliceId
                          AND state = @expected",
                    new
                    {
                        sliceid = slice.Id,
                        state = WalletSliceState.Reserved,
                        expected = WalletSliceState.Available
                    });

                if (rowsChanged != 1)
                    throw new InvalidOperationException(
                        $"Slice with id {slice.Id} could not be found or was no longer available");
            }

            return takenSlices;
        }
        else if (willBeAvailable < reserveQuantity)
        {
            throw new InvalidOperationException("Owner has less to reserve than available");
        }
        else if (willBeAvailable >= reserveQuantity)
        {
            throw new QuantityNotYetAvailableToReserveException(
                "Owner has enough quantity, but it is not yet available to reserve");
        }
        else
        {
            throw new Exception("Unexpected error");
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

    public async Task ExpireSlices(DateTimeOffset olderThanDate)
    {
        await _connection.ExecuteAsync("expire_slices",
            new
            {
                older_than_date = olderThanDate,
                expire_state_int = WalletSliceState.Expired,
                available_state_int = WalletSliceState.Available
            },
            commandType: CommandType.StoredProcedure);
    }
}
