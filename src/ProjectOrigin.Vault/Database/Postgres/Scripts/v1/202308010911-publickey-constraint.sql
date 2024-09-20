ALTER TABLE DepositEndpoints
DROP CONSTRAINT IF EXISTS walletsections_publickey_key;

CREATE UNIQUE INDEX idx_unique_publickey_walletid
ON DepositEndpoints (PublicKey)
WHERE WalletId IS NOT NULL;
