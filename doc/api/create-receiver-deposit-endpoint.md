## Receiver Deposit Endpoint
Wallet deposit endpoints are created to have an endpoint for which the sender can transfer certificates to.
These are persisted with a walletId, a position, a public key and a subject and without referenceText and endpoint.

Receiver deposit endpoints are to persists receivers in a given wallet.
These are persisted without walletId and walletposition but with public key from the owner, a subject, a reference and an enpoint.
