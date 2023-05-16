CREATE TABLE IF NOT EXISTS Registries (
    Id uuid NOT NULL PRIMARY KEY,
    Name VARCHAR(128) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Certificates (
    Id uuid NOT NULL PRIMARY KEY,
    RegistryId uuid NOT NULL,
    TechCode VARCHAR(64) NOT NULL,
    FuelCode VARCHAR(64) NOT NULL,
    StartDate TIMESTAMP with time zone NOT NULL,
    EndDate TIMESTAMP with time zone NOT NULL,
    GridArea VARCHAR(128) NOT NULL,
    Loaded BIT NOT NULL,
    FOREIGN KEY (RegistryId)
        REFERENCES Registries (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

CREATE TABLE IF NOT EXISTS Slices (
    Id uuid NOT NULL PRIMARY KEY,
    WalletSectionId uuid NOT NULL,
    SectionPosition integer NOT NULL,
    CertificateId uuid NOT NULL,
    Quantity bigint NOT NULL,
    RandomR bigint NOT NULL,
    Verified BIT NOT NULL,
    FOREIGN KEY (WalletSectionId)
        REFERENCES WalletSections (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID,
    FOREIGN KEY (CertificateId)
        REFERENCES Certificates (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);

