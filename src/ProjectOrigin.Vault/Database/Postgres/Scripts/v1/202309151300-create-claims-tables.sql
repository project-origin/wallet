CREATE TABLE claims (
    id uuid NOT NULL PRIMARY KEY,
    production_slice_id uuid NOT NULL,
    consumption_slice_id uuid NOT NULL,
    state integer NOT NULL,
    FOREIGN KEY (production_slice_id)
        REFERENCES slices (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    FOREIGN KEY (consumption_slice_id)
        REFERENCES slices (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);
