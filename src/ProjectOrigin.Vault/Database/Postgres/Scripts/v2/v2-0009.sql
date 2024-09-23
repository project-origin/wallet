ALTER TABLE "public"."certificates" ADD "withdrawn" BOOLEAN NOT NULL DEFAULT false;

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
       max(ws.updated_at) as updated_at,
       c.withdrawn
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
