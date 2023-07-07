## GC Transfer flow

wallet://mywallet.sdaugaard.dk/?exchangeToken=123515621b35252453

```mermaid
sequenceDiagram
    actor receiver as receiver
    actor sender as Sender
    participant reg as Registry
    participant senderWallet as Sender Wallet
    participant wallet as receiver Wallet

    alt new receiver
        Note over sender,receiver: Out of bounds agreement for transfer

        receiver ->>+ wallet: CreateWalletDepositEndpoint()
        wallet -->>- receiver: WalletDepositEndpoint

        receiver ->> sender: WalletDepositEndpoint (over coffee)

        sender ->>+ senderWallet: CreateReceiverDepositEndpoint(WalletDepositEndpoint, reference: string)

        senderWallet -->>- sender: Reference: Guid
    end

    sender ->> senderWallet: TransferCertificate(CertificateId, quantity, ReceiveGuid)

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

```mermaid
sequenceDiagram
    participant ex as External party
    participant wallet as Wallet system
    participant reg as Registry
    participant receiver as Receiver Wallet

    ex ->>+ wallet: Transfer(ReceiverId, quantity, certificateId)
    wallet -->>- ex: return

    wallet ->>+ wallet: Start from JobQueue

    wallet ->> wallet: set source slice to state: slicing
    wallet ->> wallet: get deposit position

    wallet ->> wallet: create two new slices
    note over wallet: "one for own deposit endpoint, one for receiver deposit endpoint, set status: registering"

    wallet ->> registry: SubmitTransaction

    alt loop until valid
        wallet ->> registry: GetTransactionStatus
    end
    wallet ->> wallet: set source slice to state: sliced
    wallet ->> wallet: new slices to state: available

    wallet ->> receiver: ReceivedSlice()

    deactivate wallet

```
