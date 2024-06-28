CREATE TABLE request_statuses (
    request_id uuid NOT NULL,
    owner VARCHAR(64) NOT NULL,
    status integer NOT NULL,
    failed_reason VARCHAR(512) NULL,
    PRIMARY KEY(request_id, owner)
);
