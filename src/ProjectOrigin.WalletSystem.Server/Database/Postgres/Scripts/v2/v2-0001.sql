-- Create v2 tables

CREATE TABLE wallets (
    id uuid NOT NULL PRIMARY KEY,
    owner VARCHAR(64) NOT NULL UNIQUE,
    private_key bytea NOT NULL
);

CREATE TABLE receive_endpoints
(
    id uuid NOT NULL PRIMARY KEY,
    wallet_id uuid NOT NULL,
    wallet_position integer NOT NULL,
    public_key bytea NOT NULL UNIQUE,
    is_remainder_endpoint boolean NOT NULL,
    UNIQUE (wallet_id, wallet_position),
    UNIQUE (public_key),
    FOREIGN KEY (wallet_id)
        REFERENCES wallets (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

CREATE UNIQUE INDEX idx_unique_remainder_endpoint
    ON receive_endpoints (wallet_id)
    WHERE is_remainder_endpoint IS TRUE;

CREATE TABLE deposit_endpoints
(
    id uuid NOT NULL PRIMARY KEY,
    owner VARCHAR(64) NOT NULL,
    public_key bytea NOT NULL UNIQUE,
    reference_text VARCHAR(256) NOT NULL,
    endpoint VARCHAR(512) NOT NULL
);

CREATE TABLE certificates (
    id uuid NOT NULL,
    registry_name VARCHAR(64) NOT NULL,
    start_date timestamp with time zone NOT NULL,
    end_date timestamp with time zone NOT NULL,
    grid_area VARCHAR(256) NOT NULL,
    certificate_type integer NOT NULL,
    PRIMARY KEY(id, registry_name)
);

CREATE TABLE attributes (
    id uuid NOT NULL PRIMARY KEY,
    key_atr VARCHAR(256) NOT NULL,
    value_atr VARCHAR(512) NOT NULL,
    certificate_id uuid NOT NULL,
    registry_name VARCHAR(64) NOT NULL,
    UNIQUE (certificate_id, registry_name, key_atr),
    FOREIGN KEY (certificate_id, registry_name)
        REFERENCES certificates (Id, registry_name) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

CREATE TABLE deposit_slices (
    id uuid NOT NULL PRIMARY KEY,
    certificate_id uuid NOT NULL,
    registry_name VARCHAR(64) NOT NULL,
    deposit_endpoint_id uuid NOT NULL,
    deposit_endpoint_position integer NOT NULL,
    slice_state integer NOT NULL,
    quantity bigint NOT NULL,
    random_r bytea NOT NULL,
    FOREIGN KEY (deposit_endpoint_id)
        REFERENCES deposit_endpoints (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    FOREIGN KEY (certificate_id, registry_name)
        REFERENCES certificates (id, registry_name) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

CREATE TABLE received_slices (
    id uuid NOT NULL PRIMARY KEY,
    certificate_id uuid NOT NULL,
    registry_name VARCHAR(64) NOT NULL,
    receive_endpoint_id uuid NOT NULL,
    receive_endpoint_position integer NOT NULL,
    slice_state integer NOT NULL,
    quantity bigint NOT NULL,
    random_r bytea NOT NULL,
    FOREIGN KEY (receive_endpoint_id)
        REFERENCES receive_endpoints (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    FOREIGN KEY (certificate_id, registry_name)
        REFERENCES certificates (id, registry_name) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

CREATE TABLE claims (
    id uuid NOT NULL PRIMARY KEY,
    production_slice_id uuid NOT NULL,
    consumption_slice_id uuid NOT NULL,
    state integer NOT NULL,
    FOREIGN KEY (production_slice_id)
        REFERENCES received_slices (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    FOREIGN KEY (consumption_slice_id)
        REFERENCES received_slices (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);
