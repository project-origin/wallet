CREATE TABLE IF NOT EXISTS Registries (
    Id uuid NOT NULL PRIMARY KEY,
    Name VARCHAR(128) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Certificates (
    Id uuid NOT NULL,
    RegistryId uuid NOT NULL,
    Loaded BOOLEAN NOT NULL,
    -- TechCode VARCHAR(64),
    -- FuelCode VARCHAR(64),
    -- StartDate TIMESTAMP with time zone,
    -- EndDate TIMESTAMP with time zone,
    -- GridArea VARCHAR(128),
    PRIMARY KEY(Id, RegistryId),
    FOREIGN KEY (RegistryId)
        REFERENCES Registries (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

CREATE TABLE IF NOT EXISTS Slices (
    Id uuid NOT NULL PRIMARY KEY,
    WalletSectionId uuid NOT NULL,
    WalletSectionPosition integer NOT NULL,
    RegistryId uuid NOT NULL,
    CertificateId uuid NOT NULL,
    Quantity bigint NOT NULL,
    RandomR bytea NOT NULL,
    Verified BOOLEAN NOT NULL,
    FOREIGN KEY (WalletSectionId)
        REFERENCES WalletSections (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    FOREIGN KEY (CertificateId, RegistryId)
        REFERENCES Certificates (Id, RegistryId) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

