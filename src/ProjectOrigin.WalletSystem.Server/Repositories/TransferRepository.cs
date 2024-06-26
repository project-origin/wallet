using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.ViewModels;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class TransferRepository : ITransferRepository
{
    private readonly IDbConnection _connection;

    public TransferRepository(IDbConnection connection) => this._connection = connection;

    public async Task InsertTransferredSlice(TransferredSlice newSlice)
    {
        await _connection.ExecuteAsync(
            @"INSERT INTO transferred_slices(id, certificate_id, registry_name, external_endpoint_id, external_endpoint_position, state, quantity, random_r)
              VALUES (@id, @certificateId, @registryName, @externalEndpointId, @externalEndpointPosition, @state, @quantity, @randomR)",
            newSlice);
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

    public async Task<PageResultCursor<TransferViewModel>> QueryTransfers(QueryTransfersFilterCursor filter)
    {
        string sql = @"
        CREATE TEMPORARY TABLE transfer_work_table ON COMMIT DROP AS (
            SELECT
                *
            FROM
                transfers_query_model
            WHERE
                owner = @owner
                AND (@start IS NULL OR start_date >= @start)
                AND (@end IS NULL OR end_date <= @end)
        );
        SELECT count(*) FROM transfer_work_table;
        SELECT * FROM transfer_work_table WHERE updated_at > @UpdatedSince LIMIT @limit;
        SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
        FROM attributes_view attributes
            WHERE (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT registry_name, certificate_id FROM transfer_work_table))
                OR (wallet_id, registry_name, certificate_id) IN (SELECT wallet_id, registry_name, certificate_id FROM transfer_work_table)
        ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, filter))
        {
            var totalCount = gridReader.ReadSingle<int>();
            var transfers = gridReader.Read<TransferViewModel>();
            var attributes = gridReader.Read<AttributeViewModel>();

            foreach (var transfer in transfers)
            {
                transfer.Attributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == transfer.RegistryName
                                   && attr.CertificateId == transfer.CertificateId));
            }

            return new PageResultCursor<TransferViewModel>
            {
                Items = transfers,
                TotalCount = totalCount,
                Count = transfers.Count(),
                UpdatedSince = filter.UpdatedSince?.ToUnixTimeSeconds(),
                Limit = filter.Limit
            };
        }
    }

    public async Task<PageResult<TransferViewModel>> QueryTransfers(QueryTransfersFilter filter)
    {
        string sql = @"
        CREATE TEMPORARY TABLE transfer_work_table ON COMMIT DROP AS (
            SELECT
                *
            FROM
                transfers_query_model
            WHERE
                owner = @owner
                AND (@start IS NULL OR start_date >= @start)
                AND (@end IS NULL OR end_date <= @end)
        );
        SELECT count(*) FROM transfer_work_table;
        SELECT * FROM transfer_work_table LIMIT @limit OFFSET @skip;
        SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
        FROM attributes_view attributes
            WHERE (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT registry_name, certificate_id FROM transfer_work_table))
                OR (wallet_id, registry_name, certificate_id) IN (SELECT wallet_id, registry_name, certificate_id FROM transfer_work_table)
        ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, filter))
        {
            var totalCount = gridReader.ReadSingle<int>();
            var transfers = gridReader.Read<TransferViewModel>();
            var attributes = gridReader.Read<AttributeViewModel>();

            foreach (var transfer in transfers)
            {
                transfer.Attributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == transfer.RegistryName
                            && attr.CertificateId == transfer.CertificateId));
            }

            return new PageResult<TransferViewModel>
            {
                Items = transfers,
                TotalCount = totalCount,
                Count = transfers.Count(),
                Offset = filter.Skip,
                Limit = filter.Limit
            };
        }
    }

    public async Task<PageResult<AggregatedTransferViewModel>> QueryAggregatedTransfers(QueryAggregatedTransfersFilter filter)
    {
        string sql = @"
        CREATE TEMPORARY TABLE transfer_work_table ON COMMIT DROP AS (
            SELECT *
            FROM (
                SELECT
                    min(start_date) as start,
                    max(end_date) as end,
                    sum(quantity) as quantity
                FROM
                    transfers_query_model
                WHERE
                    owner = @owner
                    AND (@start IS NULL OR start_date >= @start)
                    AND (@end IS NULL OR end_date <= @end)
                GROUP BY
                        CASE
                            WHEN @timeAggregate = 'total' THEN NULL
                            WHEN @timeAggregate = 'quarterhour' THEN date_trunc('hour', start_date AT TIME ZONE @timeZone) + INTERVAL '15 min' * ROUND(EXTRACT(MINUTE FROM start_date AT TIME ZONE @timeZone) / 15.0)
                            WHEN @timeAggregate = 'actual' THEN start_date AT TIME ZONE @timeZone
                            ELSE date_trunc(@timeAggregate, start_date AT TIME ZONE @timeZone)
                        END
                    ) as aggregates
            ORDER BY
                start
        );
        SELECT count(*) FROM transfer_work_table;
        SELECT * FROM transfer_work_table LIMIT @limit OFFSET @skip;
        ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, new
        {
            filter.Owner,
            filter.Start,
            filter.End,
            filter.Skip,
            filter.Limit,
            timeAggregate = filter.TimeAggregate.ToString().ToLowerInvariant(),
            filter.TimeZone,
        }))
        {
            var totalCount = gridReader.ReadSingle<int>();
            var transfers = gridReader.Read<AggregatedTransferViewModel>();

            return new PageResult<AggregatedTransferViewModel>
            {
                Items = transfers,
                TotalCount = totalCount,
                Count = transfers.Count(),
                Offset = filter.Skip,
                Limit = filter.Limit
            };
        }
    }

}
