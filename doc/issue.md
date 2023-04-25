# GC issuance flow

Below is outline of two solutions to handle issuance flows.

## Seed solution

Here the issuer is given a reference the the owner wallet, and a seed_public_key.

The Issuer can here generate new public keys in series, so no extra call to the wallet to get an public key is needed.
But, if the Owner has multiple issuers, he must give a different account(BIP44 like) to each issuer, as to ensure the same
key is not used multiple times.

```mermaid

sequenceDiagram
    actor reciever as Owner
    participant issuer as Issuer
    participant reg as Registry
    participant wallet as Owner Wallet
    participant datahub as Issuer data provider


    reciever ->> issuer: Configure wallet(wallet_ref, seed_public_key)

    Note over issuer: Later...

    datahub ->> issuer: New GC for Owner

    activate issuer
    issuer ->> issuer: Generate new public-key based on<br>public-key seed key using bip32 like scheme
    issuer ->> reg: Issue GC
    deactivate issuer

    activate reg
    reg -->> issuer: Complete
    deactivate reg

    activate issuer
    issuer->> wallet: SendInfo(accountRef, index, certId, m, r... )
    deactivate issuer

    activate wallet
    wallet ->> wallet: Update wallet with entry
    deactivate wallet

```

## Request solution

Here the issuer is only given a reference the the owner wallet.

The issuer will then request the wallet for a public-key to which to issue a GC.
This results in more requests, these could be bundled, and could result in the wallet reserving keys that never end up being used.

```mermaid

sequenceDiagram
    actor reciever as Owner
    participant issuer as Issuer
    participant reg as Registry
    participant wallet as Owner Wallet
    participant datahub as Issuer data provider


    reciever ->> issuer: Configure wallet(wallet_ref)

    Note over issuer: Later...

    datahub ->> issuer: New GC for Owner

    activate issuer
    issuer ->>+ wallet: requestPublicKey(wallet_ref)
    wallet ->>- issuer: public-key
    issuer ->> reg: Issue GC
    deactivate issuer

    activate reg
    reg -->> issuer: Complete
    deactivate reg

    activate issuer
    issuer->> wallet: AddGC(wallet_ref, certId, commitmentInfo,... )
    deactivate issuer

    activate wallet
    wallet ->> wallet: Update wallet with entry
    deactivate wallet

```
