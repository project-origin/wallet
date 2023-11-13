# Project Origin - Wallet

## Functionality

The Wallet

List of commands on a GC abstraction, all commands will be translated by the wallet to primitive slice commands.

### Public API

The public API allows anonymous access to deposit slices into wallets.

|  Name   | Description |
| - | - |
| [ReceiveSlice](../flows/receive_slice.md) | Enables a wallet to receive a slice from either an issuer or another wallet, the slice is verified based on data from the registry. |

### Wallet API

The Wallet API allows a wallet to interact with the wallet system.

All commands require the request to be authenticated by a JWT token.

| Name | Description |
| ---- | ----------- |
| CreateWallet | Creates a new wallet for a user. Only one wallet per user is currently allowed, but multiple wallets per user may be allowed in the future. |
| CreateWalletEndpoint | Creates a new endpoint in a user's [wallet](../concepts/wallet.md), which can be shared to enable transfer to the wallet. |
| CreateExternalEndpoint | Creates a reference to another users wallet, so that the wallet can send slices to the other wallet. |
| [QueryGranularCertificates](../api/query.md) | List the certificates owned by the owner, in time filters will be supported |
| TransferCertificate | Transfers a number of Wh from ther certificate from one wallet to a other wallet. |
| ClaimCertificates | Claims a number of Wh from a production certificate to a consumption certificate. |
| QueryClaims | List the claims made in a wallet. |

### Events

The wallet service should expose events to enable other services
to know what happens in the wallet as to enable them to react to it
without falling back to a polling mechanism.
