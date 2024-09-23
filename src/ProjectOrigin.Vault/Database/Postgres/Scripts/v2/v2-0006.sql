ALTER TABLE wallet_slices
    ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone DEFAULT NOW();
ALTER TABLE transferred_slices
    ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone DEFAULT NOW();
ALTER TABLE claims
    ADD COLUMN IF NOT EXISTS updated_at timestamp with time zone DEFAULT NOW();


CREATE OR REPLACE FUNCTION update_updated_at_column()
    RETURNS TRIGGER AS
$$
BEGIN
    NEW.updated_at = NOW();
    RETURN NEW;
END;
$$ language 'plpgsql';

CREATE TRIGGER update_wallet_slices_updated_at
    AFTER UPDATE
    ON wallet_slices
    FOR EACH ROW
EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_transferred_slices_updated_at
    AFTER UPDATE
    ON transferred_slices
    FOR EACH ROW
EXECUTE FUNCTION update_updated_at_column();

CREATE TRIGGER update_claims_updated_at
    AFTER UPDATE
    ON claims
    FOR EACH ROW
EXECUTE FUNCTION update_updated_at_column();

CREATE OR REPLACE VIEW certificates_query_model AS
SELECT c.id as certificate_id,
       c.registry_name,
       c.certificate_type,
       c.grid_area,
       c.start_date,
       c.end_date,
       w.id as wallet_id,
       w.owner,
       sum(CASE WHEN ws.state = 1 THEN ws.quantity ELSE 0 END) as quantity,
       max(ws.updated_at) as updated_at
FROM wallets w
         INNER JOIN wallet_endpoints we
                    ON w.id = we.wallet_id
         INNER JOIN wallet_slices ws
                    ON we.Id = ws.wallet_endpoint_id
         INNER JOIN certificates c
                    ON ws.certificate_id = c.id
GROUP BY c.id,
         c.registry_name,
         c.certificate_type,
         c.grid_area,
         c.start_date,
         c.end_date,
         w.id,
         w.owner
ORDER BY updated_at ASC,
         c.start_date ASC,
         c.id ASC;

CREATE OR REPLACE VIEW claims_query_model AS
SELECT claims.Id                 as claim_id,
       slice_cons.quantity       AS quantity,
       wallet_cons.id            as wallet_id,
       wallet_cons.owner         as owner,

       slice_prod.registry_name  AS production_registry_name,
       slice_prod.certificate_id AS production_certificate_id,
       cert_prod.start_date      AS production_start,
       cert_prod.end_date        AS production_end,
       cert_prod.grid_area       AS production_grid_area,

       slice_cons.registry_name  AS consumption_registry_name,
       slice_cons.certificate_id AS consumption_certificate_id,
       cert_cons.start_date      AS consumption_start,
       cert_cons.end_date        AS consumption_end,
       cert_cons.grid_area       AS consumption_grid_area,
       claims.updated_at         AS updated_at
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
WHERE claims.state = 10 -- Claimed
ORDER BY claims.updated_at ASC;

CREATE OR REPLACE VIEW transfers_query_model AS
SELECT c.id            AS certificate_id,
       c.registry_name AS registry_name,
       ee.id           AS receiver_id,
       c.grid_area     AS grid_area,
       ts.quantity     AS quantity,
       c.start_date    AS start_date,
       c.end_date      AS end_date,
       ee.owner        AS owner,
       ts.updated_at   AS updated_at
FROM transferred_slices ts
         INNER JOIN external_endpoints ee
                    ON ts.external_endpoint_id = ee.id
         INNER JOIN certificates c
                    ON ts.certificate_id = c.id
ORDER BY ts.updated_at ASC;
