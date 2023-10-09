-- Prepare for migration to v2

-- throw exception if ReceivedSlices is not empty
DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM ReceivedSlices LIMIT 1) THEN
        RAISE EXCEPTION 'ReceivedSlices table is not empty, use older version (max 0.2.x) of WalletSystem to empty table before upgrading.';
    END IF;
END $$;
DROP TABLE ReceivedSlices;

-- rename old tables to old_* to prepare for migration to v2
ALTER TABLE Wallets RENAME TO old_wallets;
ALTER TABLE Certificates RENAME TO old_certificates;
ALTER TABLE Attributes RENAME TO old_attributes;
ALTER TABLE Slices RENAME TO old_slices;
ALTER TABLE Claims RENAME TO old_claims;
ALTER TABLE Registries RENAME TO old_registries;
ALTER TABLE DepositEndpoints RENAME TO old_deposit_endpoints;
