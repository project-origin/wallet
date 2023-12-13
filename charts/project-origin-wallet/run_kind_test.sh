#!/bin/bash

# This script is used to test the wallet chart using kind.
# It installs the chart and validates it starts up correctly.

# Define kind cluster name
cluster_name=wallet-test

# Ensures script fails if something goes wrong.
set -eo pipefail

# cleanup - delete temp_folder and cluster
trap 'rm -fr $temp_folder; kind delete cluster -n ${cluster_name} >/dev/null 2>&1' 0

# define variables
temp_folder=$(mktemp -d)
values_filename=${temp_folder}/values.yaml

# create kind cluster
kind delete cluster -n ${cluster_name}
kind create cluster -n ${cluster_name}

# install rabbitmq-operator
kubectl apply -f "https://github.com/rabbitmq/cluster-operator/releases/download/v2.5.0/cluster-operator.yml"

# install cnpg-operator
helm install cnpg-operator cloudnative-pg --repo https://cloudnative-pg.io/charts --version 0.18.0 --namespace cnpg --create-namespace --wait

# build docker image
docker build -f src/ProjectOrigin.WalletSystem.Server/Dockerfile -t ghcr.io/project-origin/wallet-server:test src/

# load docker image into cluster
kind load -n ${cluster_name} docker-image ghcr.io/project-origin/wallet-server:test

# generate values.yaml file
cat << EOF > "${values_filename}"
image:
  tag: test

config:
  externalUrl: http://wallet.example:80
  jwt:
    allowAnyJwtToken: true

messageBroker:
  type: rabbitmqOperator
EOF

# install wallet chart
helm install wallet charts/project-origin-wallet --values ${values_filename} --namespace wallet --create-namespace --wait

echo "Test completed"
