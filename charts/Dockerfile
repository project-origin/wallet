FROM debian:bullseye-20230522 as downloader

# Install necessary dependencies
RUN apt-get update && apt-get install -y \
    curl

WORKDIR /downloads

# Download Kind
RUN arch=$(arch | sed s/aarch64/arm64/ | sed s/x86_64/amd64/) &&\
    curl -Lo ./kind  "https://kind.sigs.k8s.io/dl/v0.19.0/kind-linux-${arch}" &&\
    chmod +x ./kind

# Download Helm
RUN arch=$(arch | sed s/aarch64/arm64/ | sed s/x86_64/amd64/) &&\
    curl -Lo ./helm.tar.gz  "https://get.helm.sh/helm-v3.12.0-linux-${arch}.tar.gz" &&\
    tar -zxvf ./helm.tar.gz linux-arm64/helm --strip-components 1 &&\
    chmod +x ./helm

FROM docker:24.0.1-dind

# Copy binaries from the downloader stage
COPY --from=downloader /downloads/kind /usr/local/bin/kind
COPY --from=downloader /downloads/helm /usr/local/bin/helm
