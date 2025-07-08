-- Used to optimize queries where we need available wallet slices that have a positive quantity
CREATE INDEX IF NOT EXISTS idx_wallet_slices_filtered
ON wallet_slices(wallet_endpoint_id)
WHERE state = 1 AND quantity != 0;
