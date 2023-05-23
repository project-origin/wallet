# Slice receive flow

Granular Certificates are "transferred" as slices to the wallet,
the actual slice is on the registry,
the necessary information to prove ownership and act on the slice is transferred.

1. A source sends a slice to a recipient wallet.
2. The wallet will try to look-up the wallet-section based on the public-key.
3. If the certificate is not already known by the wallet, then a reference to the certificate is inserted into the datastore
4. Next the slice itself is inserted with reference to the wallet-section and certificate
5. And finally a success is returned
6. If the wallet-section could not be found then an error is returned.

```mermaid
sequenceDiagram
    autonumber

    participant source as Source
    participant wallet as Wallet

    source ->>+ wallet: ReceiveSlice(ReceiveRequest)

    wallet ->> wallet: get wallet-section from public-key

    alt wallet-section found

        alt Certificate not known
            wallet ->> wallet: insert Certificate
        end

        wallet ->> wallet: insert Slice

        wallet -->> source: Success
    else wallet-section not found
        wallet -->>- source: Error
    end

```

## Slice verification

> **Warning**
> Not yet implemented

When a slice is received, it is stored in the data-store as non-verified.

A background process will then verify and update the state of the slice later. If the certificate and attributes are unknown, then they will be updated as part of this flow.

```mermaid
sequenceDiagram
    autonumber

    actor sche as Scheduler
    participant wallet as Wallet
    participant reg as Registry

    link wallet: Dashboard @ https://dashboard.contoso.com/alice

    loop Every minute
        sche -->>+ wallet: VerifySlices

        wallet ->> wallet: Get unverified slices

        wallet ->> reg: Get Cert and Slice Info
        reg ->> wallet: slice info
        wallet ->> wallet: Verify Certificate issuer
        wallet ->> wallet: Verify Slice ownership
        wallet ->> wallet: Update wallet datastore
        deactivate wallet
    end
```
