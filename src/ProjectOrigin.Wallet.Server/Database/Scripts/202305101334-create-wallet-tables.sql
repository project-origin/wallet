CREATE TABLE IF NOT EXISTS Wallets (
    Id uuid NOT NULL PRIMARY KEY,
    Owner VARCHAR(64) NOT NULL UNIQUE,
    PrivateKey bytea NOT NULL
);

CREATE TABLE IF NOT EXISTS WalletSections
(
    Id uuid NOT NULL PRIMARY KEY,
    WalletId uuid NOT NULL,
    WalletPosition integer NOT NULL,
    PublicKey bytea NOT NULL UNIQUE,
    UNIQUE (WalletId, WalletPosition),
    FOREIGN KEY (WalletId)
        REFERENCES Wallets (id) MATCH SIMPLE
        ON UPDATE NO ACTION
        ON DELETE NO ACTION
        NOT VALID
);
