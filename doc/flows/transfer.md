## GC Transfer flow

```mermaid
sequenceDiagram
    actor reciever as Reciever
    actor sender as Sender
    participant reg as Registry
    participant senderWallet as Sender Wallet
    participant wallet as Reciever Wallet

    Note over sender,reciever: Out of bounds agreement for transfer
    reciever ->> sender: WalletSectionReference
    Note over senderWallet: Slice is expected to exist

    sender ->> senderWallet: TransferSlice(WalletSectionReference,...)

    activate senderWallet
    senderWallet ->> senderWallet: Generate next publicKey
    senderWallet ->> reg: call TransferSlice
    deactivate senderWallet

    activate reg
    reg -->> senderWallet: Transfer complete
    deactivate reg

    activate senderWallet
    senderWallet->> wallet: SendInfo(accountRef, index, certId, m, r... )
    deactivate senderWallet

    activate wallet
    wallet ->>+ reg: Get slice
    reg ->>- wallet: slice info
    wallet ->> wallet: verifySlice
    wallet ->> wallet: Update wallet datastore
    deactivate wallet
```
