# Programming Language: C#/.NET

## Status

Proposed

## Context

The community is developing an open-source wallet and we need to choose a programming language for it.
The wallet will be part of a larger system that includes several other services, most of which are currently written in C#/.NET.

## Decision

We have briefly considered other programming languages, but have decided to use C#/.NET as our programming language for the following reasons:

- Most other services in the system are written in C#/.NET, and using the same language will simplify integration and maintenance.

- The community has experience with C#/.NET and it is familiar to them.

- C#/.NET is a proven language with a large developer community and a rich set of libraries and frameworks.

- C#/.NET is a type-safe language with built-in support for memory management, which reduces the likelihood of errors and improves security.

- C#/.NET supports multiple platforms, including Windows, Linux, and macOS, which makes it easy to deploy and run the project in different environments.

## Consequences

The decision to use C#/.NET as our programming language will have the following consequences:

- The project will be easier to integrate and maintain because it uses the same programming language as the existing services in the system.

- Community members who are not familiar with C#/.NET will need to learn the language.

- Community members that wishes to implement the wallet features in a different language may wish to do so by using the provided .proto files and implement the features of the wallet and contribute the code if they wish to do so.
