ALTER TABLE request_statuses ADD COLUMN IF NOT EXISTS created timestamp with time zone DEFAULT NOW();
ALTER TABLE request_statuses ADD COLUMN IF NOT EXISTS type integer DEFAULT 0;
