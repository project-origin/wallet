src_path := src

docfx_config := doc/docfx.json
docfx_site_dir := doc/_site

formatting_header := \033[1m
formatting_command := \033[1;34m
formatting_desc := \033[0;32m
formatting_none := \033[0m

.PHONY: help test clean build lint

.DEFAULT_GOAL := help

## Show help for each of the Makefile recipes.
help:
	@printf "${formatting_header}Available targets:\n"
	@awk -F '## ' '/^## /{desc=$$2}/^[a-zA-Z0-9][a-zA-Z0-9_-]+:/{gsub(/:.*/, "", $$1); printf "  ${formatting_command}%-20s ${formatting_desc}%s${formatting_none}\n", $$1, desc}' $(MAKEFILE_LIST) | sort
	@printf "\n"

## Verify code is ready for commit to branch, runs tests and verifies formatting.
verify: build test lint
	@echo "Code is ready to commit."

## Prints dotnet info
info:
	@echo "Print info and version"
	dotnet --info
	dotnet --version

## Lint the dotnet code
lint:
	@echo "Verifying code formatting..."
	dotnet format $(src_path) --verify-no-changes

## Does a dotnet clean
clean:
	dotnet clean $(src_path)

## Restores all dotnet projects
restore:
	dotnet tool restore --tool-manifest src/.config/dotnet-tools.json
	dotnet restore $(src_path)

## Builds all the code
build: restore
	dotnet build $(src_path)

## Formats files using dotnet format
format:
	dotnet format $(src_path)

## Run all tests
test:
	dotnet test $(src_path)

## Run all Unit-tests
unit-test:
	dotnet test $(src_path) --filter 'FullyQualifiedName!~IntegrationTests'

## Builds the local container, creates kind cluster and installs chart, and verifies it works
verify-chart:
	@kind version >/dev/null 2>&1 || { echo >&2 "kind not installed! kind is required to use recipe, please install or use devcontainer"; exit 1;}
	@helm version >/dev/null 2>&1 || { echo >&2 "helm not installed! helm is required to use recipe, please install or use devcontainer"; exit 1;}

	kind delete cluster -n helm-test
	kind create cluster -n helm-test
	helm install cnpg-operator cloudnative-pg --repo https://cloudnative-pg.io/charts --version 0.18.0 --namespace cnpg --create-namespace --wait

	docker build -f $(src_path)/ProjectOrigin.WalletSystem.Server/Dockerfile -t ghcr.io/project-origin/wallet-server:test $(src_path)/
	kind load -n helm-test docker-image ghcr.io/project-origin/wallet-server:test
	helm install wallet charts/project-origin-wallet --set image.tag=test,wallet.externalUrl=http://wallet.example:80 --wait
	kind delete cluster -n helm-test
	
## Builds and runs docker contaier
docker-run:
	docker build -f $(src_path)/ProjectOrigin.WalletSystem.Server/Dockerfile -t ghcr.io/project-origin/wallet-server:dev $(src_path) 
	docker run ghcr.io/project-origin/wallet-server:dev
