# Wallet

## Functionality

### GC Commands

List of commands on a GC abstraction, all commands will be translated by the wallet to primitive slice commands.

- Transfer (Transfer some part of a GC to someone else)
- Claim (Claim between two of my GCs)

### Slice Commands - primitives

List of commands on GC slices

- Receive (Receive slice, verify and insert)
- Transfer (Transfer my slice too someone else)
- Slice (Slice one of my slices into more slices) (could be )
- Claim (Claim between two of my slices)

### Queries

GetGCs (filter?)

### Events??

issues

## WalletService - PoC

- Refine Arch diagrams and spec - define,
- receive call and insert slice into slice table
- pull and verify GC Header from registry (38)
- GET GC API for subject (38)
- add filters.. (40)
- CI/CD pipelines
- BIP32 nøgle håndtering

- events
