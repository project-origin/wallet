CREATE TABLE IF NOT EXISTS Attributes (
    Id uuid NOT NULL PRIMARY KEY,
    KeyAtr VARCHAR(256) NOT NULL,
    ValueAtr VARCHAR(512) NOT NULL,
    CertificateId uuid NOT NULL,
    RegistryId uuid NOT NULL,
    FOREIGN KEY (CertificateId, RegistryId)
        REFERENCES Certificates (Id, RegistryId) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);
