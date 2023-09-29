CREATE TABLE IF NOT EXISTS Registries (
    Id uuid NOT NULL PRIMARY KEY,
    Name VARCHAR(128) NOT NULL UNIQUE
);

CREATE TABLE IF NOT EXISTS Certificates (
    Id uuid NOT NULL,
    RegistryId uuid NOT NULL,
    StartDate timestamp with time zone NOT NULL,
    EndDate timestamp with time zone NOT NULL,
    GridArea VARCHAR(256) NOT NULL,
    CertificateType integer NOT NULL,
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

CREATE TABLE IF NOT EXISTS ReceivedSlices (
    Id uuid NOT NULL PRIMARY KEY,
    WalletSectionId uuid NOT NULL,
    WalletSectionPosition integer NOT NULL,
    Registry VARCHAR(128) NOT NULL,
    CertificateId uuid NOT NULL,
    Quantity bigint NOT NULL,
    RandomR bytea NOT NULL,
    FOREIGN KEY (WalletSectionId)
        REFERENCES WalletSections (Id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);
