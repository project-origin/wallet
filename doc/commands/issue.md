# GC issuance flow

In the issuance flow, the owner of the asset configures the Issuer with a WalletSectionReference which contains.

The Issuer can here generate new public keys in series, so no extra call to the wallet to get an public key is needed.
But, if the Owner has multiple issuers, he must give a different walletSection to each issuer, as to ensure the same key is not used multiple times.

```mermaid
sequenceDiagram
    actor reciever as Owner
    participant issuer as Issuer
    participant reg as Registry
    participant wallet as Owner Wallet
    participant datahub as Issuer data provider

    reciever ->> issuer: ConfigureWallet(WalletSectionReference)
    Note over issuer: Later...

    datahub ->> issuer: New GC for Owner

    activate issuer
    issuer ->> issuer: Generate next publicKey
    issuer ->> reg: Issue GC
    deactivate issuer

    activate reg
    reg -->> issuer: Complete
    deactivate reg

    activate issuer
    issuer->> wallet: ReceiveRequest(accountRef, index, certId, m, r... )
    deactivate issuer

    activate wallet
    wallet ->>+ reg: Get slice
    reg ->>- wallet: slice info
    wallet ->> wallet: verifySlice
    wallet ->> wallet: Update wallet datastore
    deactivate wallet
```
