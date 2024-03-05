-- Modify the wallets table to remove the unique constraint on the owner column
ALTER TABLE wallets DROP CONSTRAINT wallets_owner_key1;
