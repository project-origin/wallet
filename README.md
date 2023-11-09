# Project Origin - Wallet

OpenSource project to create a Wallet to hold Granular Certificates in the registries.

## Pat-Let

In the Project-Origin implementation, a Granular Certificate (GC) and its Slices can never leave a registry (they have their lifecycle in one single registry).
Since owners would want the ability to collect and store their ownership of all GC slices in one place, the concept of them **having a wallet to hold their ownership across registries** make sence.

## Problem

In the Project-Origin implementation, a GC and its slices and their lifecycle remains in one single registry, but each user might hold GC slices in multiple registries. This was done to enable throughput by removing the need for consensus, which is the usual way to solve double spending issues on a DLT (Distributed Ledger Technology).

Ownership of a GC slice on a registry is proved by being in control of the private-key where the public part is registered on the slice.
For a registry, an ownership transfer is a simple change of public-key.

Users want to be able to have one place (at one service provider) to store all their digital assets (GCs and slices).
They want to interact with the system as a whole, without having the need to understand the underlying infrastructure and architecture.

The users would also want the ability to transfer the ownership of said digital asset to other parties that might be in different countries or having wallets at a different service provider.

## Context

Users might hold assets that generate GCs in multiple countries, and have been transferred GCs, which mean their GCs will be split among multiple registries, issuing bodies and service providers.

Any actor could set up an instance of a Project Origin registry.
Registries are not necessarily tied to one exact area (country or bidding zone): A registry can span multiple areas, and multiple registries can exist in one area.

## Forces

The business requirement of being able to **transfer ownership cross-border** and **claim across multiple registries**.

There is no centralized identity system between the parties, so no single identity can be used to store ownerships of certificates.

Users should be allowed to be in control of their data, and have the ability to to chose a provider manage to their data.

Actors operating as or on behalf of the state, who issue GCs (e.g. issuing bodies) needs to create or enable services that serve all consumers and producers of energy, not just the digital native.

All parties are underlying EU GDPR and possibly local legislation for privacy issues.

It would not be in the interest of the parties having to create local accounts for owners from another country, when ownership of a GC changes.
Similar banks don't create local accounts when money is transferred to an entity in another country.

## Solution

The **wallet concept** could solve this problem.

The wallet will enable the individual GC slice to always remain in one of the different registries, while enabling the user to have a wallet placed in a chosen location.
It will give the users access to view and control their assets.

## Sketch

Below is a C4 system diagram of an overview of the system landscape the wallet would be a part of.

![C4 system diagram of the wallet](./doc/wallet-c4-system.drawio.svg)

## Resulting Context

Users are now able to have access to all their GC slices from one wallet even if they are spread on multiple registries, issued by different issuers and in different countries.

Users are also enabled to choose their own wallet provider or host their own.
