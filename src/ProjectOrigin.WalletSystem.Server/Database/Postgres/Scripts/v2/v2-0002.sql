-- migrate data from old tables to new tables V2 and remove old tables

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_wallets') THEN
        INSERT INTO wallets (id, owner, private_key)
        SELECT old.Id, old.Owner, old.PrivateKey
        FROM old_wallets AS old;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_deposit_endpoints') THEN
        INSERT INTO external_endpoints (id, owner, public_key, reference_text, endpoint)
        SELECT old.Id, old.Owner, old.PublicKey, old.ReferenceText, old.Endpoint
        FROM old_deposit_endpoints AS old
        WHERE old.WalletId IS NULL;

        INSERT INTO wallet_endpoints (id, wallet_id, wallet_position, public_key, is_remainder_endpoint)
        SELECT old.Id, old.WalletId, old.WalletPosition, old.PublicKey, (old.ReferenceText = 'RemainderSection')
        FROM old_deposit_endpoints AS old
        WHERE old.WalletId IS NOT NULL;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_certificates') THEN
        INSERT INTO certificates (id, registry_name, start_date, end_date, grid_area, certificate_type)
        SELECT old.Id, old_registries.Name, old.StartDate, old.EndDate, old.GridArea, old.CertificateType
        FROM old_certificates AS old
        INNER JOIN old_registries ON old.RegistryId = old_registries.id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_attributes') THEN
        INSERT INTO attributes (id, attribute_key, attribute_value, certificate_id, registry_name)
        SELECT old.Id, old.KeyAtr, old.ValueAtr, old.CertificateId, old_registries.Name
        FROM old_attributes AS old
        INNER JOIN old_registries ON old.RegistryId = old_registries.id;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_slices') THEN
        INSERT INTO wallet_slices (id, wallet_endpoint_id, wallet_endpoint_position, state, registry_name, certificate_id, quantity, random_r)
        SELECT old.Id, old.DepositEndpointId, old.DepositEndpointPosition, old.SliceState, old_registries.Name, old.CertificateId, old.Quantity, old.RandomR
        FROM old_slices AS old
        INNER JOIN old_registries ON old.RegistryId = old_registries.id
        INNER JOIN old_deposit_endpoints ON old.DepositEndpointId = old_deposit_endpoints.id
        WHERE
            old_deposit_endpoints.WalletId IS NOT NULL;

        INSERT INTO transferred_slices (id, external_endpoint_id, external_endpoint_position, state, registry_name, certificate_id, quantity, random_r)
        SELECT old.Id, old.DepositEndpointId, old.DepositEndpointPosition, old.SliceState, old_registries.Name, old.CertificateId, old.Quantity, old.RandomR
        FROM old_slices AS old
        INNER JOIN old_registries ON old.RegistryId = old_registries.id
        INNER JOIN old_deposit_endpoints ON old.DepositEndpointId = old_deposit_endpoints.id
        WHERE
            old_deposit_endpoints.WalletId IS NULL;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_claims') THEN
        INSERT INTO claims (id, production_slice_id, consumption_slice_id, state)
        SELECT old.Id, old.production_slice_id, old.consumption_slice_id, old.state
        FROM old_claims AS old;
    END IF;

    -- Drop tables in reverse order to avoid foreign key constraints
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_claims') THEN
        DROP TABLE old_claims;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_slices') THEN
        DROP TABLE old_slices;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_attributes') THEN
        DROP TABLE old_attributes;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_certificates') THEN
        DROP TABLE old_certificates;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_registries') THEN
        DROP TABLE old_registries;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_deposit_endpoints') THEN
        DROP TABLE old_deposit_endpoints;
    END IF;

    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_name = 'old_wallets') THEN
        DROP TABLE old_wallets;
    END IF;
END $$;
