CREATE TABLE withdrawn_cursors (
    stamp_name VARCHAR(256) NOT NULL,
    sync_position integer NOT NULL,
    last_sync_date timestamp with time zone NOT NULL,
    PRIMARY KEY (stamp_name)
);
