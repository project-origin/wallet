-- Add type column to attributes table
ALTER TABLE attributes ADD COLUMN attribute_type integer NOT NULL DEFAULT 0;
ALTER TABLE attributes ALTER COLUMN attribute_type DROP DEFAULT;

-- Add wallet-attribute table
CREATE TABLE wallet_attributes (
    id uuid NOT NULL PRIMARY KEY,
    wallet_id uuid NOT NULL,
    certificate_id uuid NOT NULL,
    registry_name VARCHAR(64) NOT NULL,
    attribute_key VARCHAR(256) NOT NULL,
    attribute_value VARCHAR(512) NOT NULL,
    salt bytea NOT NULL,
    UNIQUE (wallet_id, certificate_id, registry_name, attribute_key),
    FOREIGN KEY (wallet_id)
        REFERENCES wallets (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

CREATE VIEW attributes_view AS
    SELECT
        a.certificate_id,
        a.registry_name,
        a.attribute_key,
        a.attribute_type,
        COALESCE(wa.attribute_value, a.attribute_value) AS attribute_value,
        wa.wallet_id
    FROM attributes a
    LEFT JOIN wallet_attributes wa ON a.certificate_id = wa.certificate_id
        AND a.registry_name = wa.registry_name
        AND a.attribute_key = wa.attribute_key;
