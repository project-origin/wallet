using System;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using ProjectOrigin.WalletSystem.Server.Models;
using ProjectOrigin.WalletSystem.Server.ViewModels;

namespace ProjectOrigin.WalletSystem.Server.Repositories;

public class ClaimRepository : IClaimRepository
{
    private readonly IDbConnection _connection;

    public ClaimRepository(IDbConnection connection) => this._connection = connection;

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

    public async Task<PageResultCursor<ClaimViewModel>> QueryClaimsCursor(QueryClaimsFilterCursor filter)
    {
        string sql = @"
        CREATE TEMPORARY TABLE claims_work_table ON COMMIT DROP AS (
            SELECT
                *
            FROM
                claims_query_model
            WHERE
                owner = @owner
                AND (@start IS NULL OR production_start >= @start)
                AND (@end IS NULL OR production_end <= @end)
                AND (@start IS NULL OR consumption_start >= @start)
                AND (@end IS NULL OR consumption_end <= @end)
        );
        SELECT count(*) FROM claims_work_table;
        SELECT * FROM claims_work_table LIMIT @limit updated_at > @UpdatedSince ;
        SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
        FROM attributes_view attributes
        WHERE (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT consumption_registry_name, consumption_certificate_id FROM claims_work_table))
           OR (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT production_registry_name, production_certificate_id FROM claims_work_table))
           OR (registry_name, certificate_id, wallet_id) IN (SELECT consumption_registry_name, consumption_certificate_id, wallet_id FROM claims_work_table)
           OR (registry_name, certificate_id, wallet_id) IN (SELECT production_registry_name, production_certificate_id, wallet_id FROM claims_work_table);
        ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, filter))
        {
            var totalCount = gridReader.ReadSingle<int>();
            var claims = gridReader.Read<ClaimViewModel>();
            var attributes = gridReader.Read<AttributeViewModel>();

            foreach (var claim in claims)
            {
                claim.ProductionAttributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == claim.ProductionRegistryName
                                   && attr.CertificateId == claim.ProductionCertificateId));

                claim.ConsumptionAttributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == claim.ConsumptionRegistryName
                                   && attr.CertificateId == claim.ConsumptionCertificateId));
            }

            return new PageResultCursor<ClaimViewModel>
            {
                Items = claims,
                TotalCount = totalCount,
                Count = claims.Count(),
                UpdatedSince = filter.UpdatedSince,
                Limit = filter.Limit
            };
        }
    }

    public async Task<PageResult<ClaimViewModel>> QueryClaims(QueryClaimsFilter filter)
    {
        string sql = @"
        CREATE TEMPORARY TABLE claims_work_table ON COMMIT DROP AS (
            SELECT
                *
            FROM
                claims_query_model
            WHERE
                owner = @owner
                AND (@start IS NULL OR production_start >= @start)
                AND (@end IS NULL OR production_end <= @end)
                AND (@start IS NULL OR consumption_start >= @start)
                AND (@end IS NULL OR consumption_end <= @end)
        );
        SELECT count(*) FROM claims_work_table;
        SELECT * FROM claims_work_table LIMIT @limit OFFSET @skip;
        SELECT attributes.registry_name, attributes.certificate_id, attributes.attribute_key as key, attributes.attribute_value as value, attributes.attribute_type as type
        FROM attributes_view attributes
        WHERE (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT consumption_registry_name, consumption_certificate_id FROM claims_work_table))
           OR (wallet_id IS NULL AND (registry_name, certificate_id) IN (SELECT production_registry_name, production_certificate_id FROM claims_work_table))
           OR (registry_name, certificate_id, wallet_id) IN (SELECT consumption_registry_name, consumption_certificate_id, wallet_id FROM claims_work_table)
           OR (registry_name, certificate_id, wallet_id) IN (SELECT production_registry_name, production_certificate_id, wallet_id FROM claims_work_table);
        ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, filter))
        {
            var totalCount = gridReader.ReadSingle<int>();
            var claims = gridReader.Read<ClaimViewModel>();
            var attributes = gridReader.Read<AttributeViewModel>();

            foreach (var claim in claims)
            {
                claim.ProductionAttributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == claim.ProductionRegistryName
                                   && attr.CertificateId == claim.ProductionCertificateId));

                claim.ConsumptionAttributes.AddRange(attributes
                    .Where(attr => attr.RegistryName == claim.ConsumptionRegistryName
                                   && attr.CertificateId == claim.ConsumptionCertificateId));
            }

            return new PageResult<ClaimViewModel>
            {
                Items = claims,
                TotalCount = totalCount,
                Count = claims.Count(),
                Offset = filter.Skip,
                Limit = filter.Limit
            };
        }
    }

    public async Task<PageResult<AggregatedClaimViewModel>> QueryAggregatedClaims(QueryAggregatedClaimsFilter filter)
    {
        string sql = @"
            CREATE TEMPORARY TABLE certificates_work_table ON COMMIT DROP AS (
                SELECT *
                FROM (
                    SELECT
                        min(production_start) as start,
                        max(production_end) as end,
                        sum(quantity) as quantity
                    FROM
                        claims_query_model
                    WHERE
                        owner = @owner
                        AND (@start IS NULL OR production_start >= @start)
                        AND (@end IS NULL OR production_end <= @end)
                        AND (@start IS NULL OR consumption_start >= @start)
                        AND (@end IS NULL OR consumption_end <= @end)
                    GROUP BY
                        CASE
                            WHEN @timeAggregate = 'total' THEN NULL
                            WHEN @timeAggregate = 'quarterhour' THEN date_trunc('hour', production_start AT TIME ZONE @timeZone) + INTERVAL '15 min' * ROUND(EXTRACT(MINUTE FROM production_start AT TIME ZONE @timeZone) / 15.0)
                            WHEN @timeAggregate = 'actual' THEN production_start AT TIME ZONE @timeZone
                            ELSE date_trunc(@timeAggregate, production_start AT TIME ZONE @timeZone)
                        END
                    ) as aggregates
                ORDER BY
                    start
            );
            SELECT count(*) FROM certificates_work_table;
            SELECT * FROM certificates_work_table LIMIT @limit OFFSET @skip;
            ";

        using (var gridReader = await _connection.QueryMultipleAsync(sql, new
               {
                   filter.Owner,
                   filter.Start,
                   filter.End,
                   filter.Skip,
                   filter.Limit,
                   timeAggregate = filter.TimeAggregate.ToString().ToLower(),
                   filter.TimeZone
               }))
        {
            var totalCount = gridReader.ReadSingle<int>();
            var claims = gridReader.Read<AggregatedClaimViewModel>();

            return new PageResult<AggregatedClaimViewModel>
            {
                Items = claims,
                TotalCount = totalCount,
                Count = claims.Count(),
                Offset = filter.Skip,
                Limit = filter.Limit
            };
        }
    }
}
