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
