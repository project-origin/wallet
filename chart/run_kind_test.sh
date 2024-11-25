#!/bin/bash

# This script is used to test the wallet chart using kind.
# It installs the chart and validates it starts up correctly.

# Define kind cluster name
cluster_name=vault-test
helm_install_name=vault
temp_folder=$(mktemp -d)
values_filename=${temp_folder}/values.yaml

# define cleanup function
cleanup() {
    rm -fr $temp_folder
    kind delete cluster --name ${cluster_name} >/dev/null 2>&1
}

# define debug function
debug() {
    echo -e "\nDebugging information:"
    echo -e "\nHelm list:"
    helm list --kube-context kind-${cluster_name}

    echo -e "\nHelm Status:"
    helm status ${helm_install_name} --show-desc --show-resources --kube-context kind-${cluster_name}
}

# trap cleanup function on script exit
trap 'cleanup' 0
trap 'debug; cleanup' ERR

# create kind cluster - async
kind delete cluster -n ${cluster_name}
kind create cluster -n ${cluster_name} &

# build docker image - async
make build-container &

# wait for cluster and container to be ready
wait

# load docker image into cluster
kind load -n ${cluster_name} docker-image ghcr.io/project-origin/vault:test

# install postgresql and rabbitmq
helm install postgresql oci://registry-1.docker.io/bitnamicharts/postgresql --version 15.5.23 --kube-context kind-${cluster_name}
helm install rabbitmq oci://registry-1.docker.io/bitnamicharts/rabbitmq --version 14.6.6 --kube-context kind-${cluster_name}

# generate keys
PrivateKey=$(openssl genpkey -algorithm ED25519)
PrivateKeyBase64=$(echo "$PrivateKey" | base64 -w 0)
PublicKeyBase64=$(echo "$PrivateKey" | openssl pkey -pubout | base64 -w 0)

# generate values.yaml file
cat << EOF > "${values_filename}"
image:
  tag: test

config:
  externalUrl: http://vault.example.com:80
  auth:
    jwt:
      allowAnyJwtToken: true

postgresql:
  host: postgresql
  database: postgres
  username: postgres
  port: 5432
  password:
    secretRef:
      name: postgresql
      key: postgres-password

rabbitmq:
  host: rabbitmq
  username: user
  port: 5672
  password:
    secretRef:
      name: rabbitmq
      key: rabbitmq-password

networkConfig:
  yaml: |-
    registries:
      example-registry:
        url: http://example-registry:5000
    areas:
      Narnia:
        issuerKeys:
          - publicKey: $PublicKeyBase64
EOF

# install wallet chart
helm install ${helm_install_name} chart --values ${values_filename} --wait
kubectl wait --for=condition=available --timeout=300s deployment/${helm_install_name} --context kind-${cluster_name}

echo "Test completed"
