# Dapper as Micro-ORM

## Status

Accepted

## Context

The wallet requires efficient database access and we're currently evaluating various ORM libraries to accomplish this.
We have identified the need for a ORM that allows for easy querying, parameterization, and mapping of data to POCO classes.

## Decision

After evaluating various ORM libraries, we propose to use Dapper as our ORM.
Dapper is a lightweight, open-source micro-ORM that is optimized for performance and supports multiple database providers.

We have considered Entity Framework as an alternative ORM.
However, we have decided not to use it due to its added complexity and size that we do not currently need.

We propose to use Dapper for the following reasons:

- Dapper provides efficient database access by minimizing the overhead of object-relational mapping and database access code.

- Dapper is easy to use and understand, with a simple API that allows for easy querying, parameterization, and mapping of data to POCO classes.

- Dapper supports multiple database providers, making it easier to switch between databases if necessary.

## Consequences

The decision to use Dapper as our micro-ORM will have the following consequences:

- It will simplify our database access code and reduce the amount of boilerplate code required.

- It will improve the performance of the wallet by reducing the overhead of object-relational mapping and database access code.

- It will require developers to learn the Dapper API, which may take some time and effort.

Overall, we believe that the benefits of using Dapper as our ORM outweigh the potential costs and will improve the performance and maintainability of the wallet.
