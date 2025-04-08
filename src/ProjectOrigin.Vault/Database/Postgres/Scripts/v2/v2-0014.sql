ALTER TABLE wallets ADD COLUMN IF NOT EXISTS disabled timestamp with time zone DEFAULT NULL;
