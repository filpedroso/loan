# Loan app. Everything runs in Docker. On Windows, run make from WSL.
.DEFAULT_GOAL := help
.PHONY: help doctor up down test load

ifeq ($(OS),Windows_NT)
$(error On Windows, run make from WSL (Ubuntu); see README.md)
endif

COMPOSE := docker compose -f infra/docker-compose.yml

ifeq ($(shell uname -s),Darwin)
  PLATFORM := macOS
  INSTALL_DOCKER := brew install --cask docker
  INSTALL_DOTNET := brew install --cask dotnet-sdk
  INSTALL_NODE := brew install node@24
else
  PLATFORM := $(if $(shell grep -qi microsoft /proc/version 2>/dev/null && echo wsl),Linux (WSL),Linux)
  INSTALL_DOCKER := see https://docs.docker.com/engine/install/ (on WSL also Docker Desktop with WSL integration)
  INSTALL_DOTNET := curl -sSL https://dot.net/v1/dotnet-install.sh | bash -s -- --channel 10.0 (then add ~/.dotnet to PATH)
  INSTALL_NODE := nvm install 24 (nvm: https://github.com/nvm-sh/nvm)
endif

help: ## List the targets
	@grep -E '^[a-z]+:.*## ' $(MAKEFILE_LIST) | awk -F':.*## ' '{printf "  make %-7s %s\n", $$1, $$2}'

doctor: ## Check the tools this project needs, and print how to install missing ones
	@echo "Platform: $(PLATFORM)"
	@if command -v docker >/dev/null 2>&1; then echo "  ok       docker"; else echo "  missing  docker: $(INSTALL_DOCKER)"; fi
	@if docker compose version >/dev/null 2>&1; then echo "  ok       docker compose"; else echo "  missing  docker compose: $(INSTALL_DOCKER)"; fi
	@if dotnet --list-sdks 2>/dev/null | grep -q '^10\.'; then echo "  ok       .NET 10 SDK"; else echo "  missing  .NET 10 SDK (only for make test): $(INSTALL_DOTNET)"; fi
	@if command -v node >/dev/null 2>&1; then echo "  ok       node"; else echo "  missing  node (only for frontend work): $(INSTALL_NODE)"; fi

up: ## Build and start everything
	$(COMPOSE) up -d --build
	@echo ""
	@echo "  Web app      http://localhost:3000"
	@echo "  API          http://localhost:8080/health"
	@echo "  Partner mock http://localhost:8081/applications"
	@echo "  Dashboard    http://localhost:18888"

down: ## Stop everything (keeps the database)
	$(COMPOSE) down

test: ## Run unit and integration tests (needs the .NET 10 SDK and Docker)
	cd backend && dotnet test --solution Loans.slnx

load: export RATE_LIMIT = 1000000
load: ## Load test the API with k6 (raises its rate limit until the next make up)
	$(COMPOSE) up -d api
	$(COMPOSE) --profile load run --rm k6
