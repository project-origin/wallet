# Use of Structured Logging Output as JSON

## Status

Accepted

## Context

Our wallet needs to be easily monitored and supported by multiple tools. Traditional logging output may not be sufficient for this purpose, as it can be difficult to parse and analyze.

To ensure that our project can be easily monitored and support, we have considered using structured logging output as JSON.

## Decision

After careful consideration, we propose using structured logging output as JSON in our wallet project.
This approach will provide the following benefits:

- JSON is a widely used and easily parsable format that can be analyzed by multiple monitoring and support tools.

- Structured logging allows us to add additional metadata to log messages, such as request and response data, that can be useful for troubleshooting and analysis.

Specifically, we propose the following approach:

- The Serilog library will be used to provide structured logging in our project.

- The output format for the logs will be JSON, which will be easily parsable and analyzable by multiple monitoring and support tools.

By using structured logging output as JSON, we will be able to easily monitor and support our wallet project using multiple tools.

## Consequences

The decision to use structured logging output as JSON will have the following consequences:

- The implementation of structured logging may add some additional complexity to the codebase and require additional time for development and testing.

- The output format for the logs will be JSON, which may require additional configuration and setup for some monitoring and support tools.

- The use of structured logging will provide additional metadata to log messages, which may be useful for troubleshooting and analysis.

Overall, we believe that using structured logging output as JSON is a good choice for our wallet project, given its need for easy monitoring and support by multiple tools.
