CREATE INDEX IF NOT EXISTS idx_claims_state_updated_at
    ON claims (state, updated_at);
