-- Add type certificate_view
CREATE VIEW certificates_query_model AS
    SELECT
        c.id as certificate_id,
        c.registry_name,
        c.certificate_type,
        c.grid_area,
        c.start_date,
        c.end_date,
        w.id as wallet_id,
        w.owner,
        sum(ws.quantity) AS quantity
    FROM wallets w
    INNER JOIN wallet_endpoints we
        ON w.id = we.wallet_id
    INNER JOIN wallet_slices ws
        ON we.Id = ws.wallet_endpoint_id
    INNER JOIN certificates c
        ON ws.certificate_id = c.id
    WHERE ws.state = 1 -- Available
    GROUP BY
        c.id,
        c.registry_name,
        c.certificate_type,
        c.grid_area,
        c.start_date,
        c.end_date,
        w.id,
        w.owner
    ORDER BY
        c.start_date ASC,
        c.id ASC;


CREATE VIEW claims_query_model AS
    SELECT
        claims.Id as claim_id,
        slice_cons.quantity AS quantity,
        wallet_cons.id as wallet_id,
        wallet_cons.owner as owner,

        slice_prod.registry_name AS production_registry_name,
        slice_prod.certificate_id AS production_certificate_id,
        cert_prod.start_date AS production_start,
        cert_prod.end_date AS production_end,
        cert_prod.grid_area AS production_grid_area,

        slice_cons.registry_name AS consumption_registry_name,
        slice_cons.certificate_id AS consumption_certificate_id,
        cert_cons.start_date AS consumption_start,
        cert_cons.end_date AS consumption_end,
        cert_cons.grid_area AS consumption_grid_area

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
        claims.state = 10 -- Claimed
