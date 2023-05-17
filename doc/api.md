# Project Origin - Wallet

## Functionality

The Wallet

List of commands on a GC abstraction, all commands will be translated by the wallet to primitive slice commands.

### Public API

The public API allows anonymous access to deposit slices into wallets.

|  Name   | Description |
| - | - |
| ReceiveSlice | Enables a wallet to receive a slice from either an issuer or another wallet, the slice is verified based on data fromxw the registry. |

### Granular Certificate API

The granular certificate API allows a wallet to interact with a granular certificate (GC) abstraction.
On this API the commands are translated to primitive slice commands.

All commands require the request to be authenticated by a JWT token.

| Name | Description |
| ---- | ----------- |
| CreateWallet | Creates a new wallet for a user. Only one wallet per user is currently allowed, but multiple wallets per user may be allowed in the future. |
| CreateWalletSection | Creates a new section in a user's [wallet](concepts/wallet.md). |
| QueryGranularCertificates | List the certificates owned by the wallet, in time filters should be supported |
| TransferCertificate | Transfers a number of Wh from ther certificate from one wallet to another. |
| ClaimCertificates | Claims a number of Wh from a production certificate to a consumption certificate. |

### Slice API

The slice API allows one to interact with slices directly,
this will be implemented later.

All commands require the request to be authenticated by a JWT token.

| Name | Description |
| ---- | ----------- |
| QuerySlices | Queries a user's slices. |
| CreateSlice | Creates a new slice. |
| ClaimSlice | Claims ownership of a slice. |
| TransferSlice | Transfers a slice from one wallet to another. |

### Events

The wallet service should expose events to enable other services
to know what happens in the wallet as to enable them to react to it
without falling back to a polling mechanism.
