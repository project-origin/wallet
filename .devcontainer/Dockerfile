FROM mcr.microsoft.com/devcontainers/dotnet:9.0-noble

# Install prerequisites
RUN apt-get update && apt-get install -y wget ca-certificates

# Download and install the .NET 9.0.200 SDK tarball (example link; you'll need the actual URL)
# Check https://dotnet.microsoft.com/en-us/download/dotnet for the correct tar.gz link.
RUN wget -O /tmp/dotnet.tar.gz https://download.visualstudio.microsoft.com/download/pr/<some-guid>/dotnet-sdk-9.0.200-linux-x64.tar.gz \
    && mkdir -p /usr/share/dotnet \
    && tar -xzf /tmp/dotnet.tar.gz -C /usr/share/dotnet \
    && rm /tmp/dotnet.tar.gz

# Ensure the dotnet CLI is on PATH
ENV DOTNET_ROOT=/usr/share/dotnet
ENV PATH="$PATH:/usr/share/dotnet"
