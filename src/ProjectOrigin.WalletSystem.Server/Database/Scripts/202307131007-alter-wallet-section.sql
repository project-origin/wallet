begin;
    ALTER TABLE WalletSections RENAME TO DepositEndpoints;
    ALTER TABLE DepositEndpoints ALTER COLUMN WalletId DROP NOT NULL;
    ALTER TABLE DepositEndpoints ALTER COLUMN WalletPosition DROP NOT NULL;

    ALTER TABLE DepositEndpoints ADD COLUMN Owner VARCHAR(256) NULL;
    ALTER TABLE DepositEndpoints ADD COLUMN ReferenceText VARCHAR(256) NULL;

    UPDATE DepositEndpoints
    SET Owner = w.Owner
    FROM Wallets w
    WHERE w.Id = WalletId;

	ALTER TABLE DepositEndpoints ALTER COLUMN Owner SET NOT NULL;

    UPDATE DepositEndpoints SET ReferenceText = '';
    ALTER TABLE DepositEndpoints ALTER COLUMN ReferenceText SET NOT NULL;
commit;
