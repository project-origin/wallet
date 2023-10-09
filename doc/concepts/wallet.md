# Wallets

A wallet is a fundamental component within the system that enables users to store and manage their ownership of digital assets, specifically Granular Certificates (GCs) and their corresponding slices.
It serves as a centralized repository for users to access, view, and control their assets across multiple registries, issuers, and countries.

A wallet is uniquely identified by a WalletId and is associated with a specific owner entity. The owner's identity is represented by the Owner field.
The wallet also holds the private key of the wallet.
The private key is used for cryptographic operations related to ownership verification and transaction signing.

All calls to the system are done with a JWT (Json Web Token) and enables the system to identify the current user.

An owner will be able to have multiple wallets, and authorize other users to access their wallets on a per-wallet basis.

## Wallet Endpoint

Wallet endpoints are utilized within the system to enable secure asset transfers and ownership management.
Each wallet endpoint is uniquely identified by an endpointId and is associated with a specific wallet through a foreign key reference (WalletId) and a WalletPosition.

### Purpose of Wallet Endpoints

The registries use public-private key-pairs to represent the ownership of GCs. The public key is used to verify the ownership of a slice, while the private key is used to sign transactions.
To ensure privacy and security each slice must be associated with a unique public key.

To enable the transfer of slices without having to query the wallet-system before each insert to get unique public keys, the wallet is divided into endpoints that use hierarchical deterministic keys (HD Keys) to generate unique public keys for each slice.

This way a public-key for a endpoint can be shared to other parties, and they can generate unique public keys for each slice within that endpoint without having to query the wallet-system.

The owner of the wallet can generate the corresponding private keys for each slice within a endpoint using their private key stored in their wallet.
This approach provides a level of privacy and control as the other party only knows the public key of the endpoint.

## Hierarchical Deterministic Keys (HD Keys)

Wallets and endpoints employ Hierarchical Deterministic Keys (HD Keys) to ensure the uniqueness and security of keys used for each slice.
HD Keys allow for the generation of a sequence of public keys from a single master private key. With HD Keys, each wallet endpoints has a unique public key derived from the corresponding private key. This enables secure ownership verification of the slices within that endpoint.

Wallet endpoints, combined with the use of HD Keys, provide users with a secure and convenient way to manage their ownership of GCs within the system. By sharing the public key of a specific endpoint, owners can receive assets into that endpoint while maintaining control over their private key. This allows for seamless asset transfers and ownership management across different registries and issuers.

## Abstraction

The wallet and wallet endpoints abstract the underlying infrastructure and architecture, providing users with a unified interface to interact with their digital assets. Users can query their certificates, transfer ownership, and perform other operations without the need to understand the intricate details of the system's internals.

The wallet and wallet endpoints play a crucial role in enabling users to have a comprehensive view of their digital assets, regardless of the number of registries, issuers, or countries involved. They provide a secure and user-centric approach to asset management within the system.
