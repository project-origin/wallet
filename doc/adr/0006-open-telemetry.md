# Use of OpenTelemetry for Metrics and Tracing

## Status

Proposed

## Context

Our open-source wallet project needs to be easily monitored and traced for performance issues.
To achieve this, we have considered using a metrics and tracing system that is compatible with our project architecture.

After evaluating various options, we propose using OpenTelemetry as the metrics and tracing system for our wallet project.

## Decision

After careful consideration, we propose using OpenTelemetry for metrics and tracing in our wallet project. This approach will provide the following benefits:

- OpenTelemetry is an open-source, vendor-neutral project that provides a single set of APIs and libraries for metrics and tracing.

- OpenTelemetry provides a wide range of integrations for popular libraries and frameworks, making it easy to get started with.

- OpenTelemetry provides a powerful set of features for metrics and tracing, including distributed tracing, context propagation, and support for multiple data stores.

By using OpenTelemetry, we will be able to easily monitor and trace our wallet project, as well as gain insights into performance issues that may arise.

## Consequences

The decision to use OpenTelemetry will have the following consequences:

- The implementation of OpenTelemetry may add some additional complexity to the codebase and require additional time for development and testing.

- OpenTelemetry will require additional configuration and setup, as well as the installation of necessary libraries and frameworks.

- OpenTelemetry will provide a powerful set of features for metrics and tracing, including distributed tracing and context propagation, which may be useful for identifying and diagnosing performance issues.

Overall, we believe that using OpenTelemetry for metrics and tracing is a good choice for our wallet project, given its need for easy monitoring and tracing of performance issues.
