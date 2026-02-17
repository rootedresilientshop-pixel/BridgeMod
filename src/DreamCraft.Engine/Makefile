# DreamCraft: Legacies v2 — Makefile
#
# Usage:
#   make up            Build and start the simulation container
#   make down          Stop the container
#   make logs          Tail engine logs
#   make health        Check API and DB health
#   make vault-sync    Pull latest export from Oracle to Pi 5 SSD
#   make backup        Timestamped backup of saga.db to ./backups/

COMPOSE      := docker compose
CONTAINER    := dreamcraft-api
DB_PATH      := data/saga.db
BACKUP_DIR   := backups
API_URL      := http://localhost:8002/api/health

# Oracle vault-sync settings — override via environment or .env
ORACLE_USER  ?= ubuntu
ORACLE_HOST  ?= $(shell grep SAGA_ORACLE_HOST .env 2>/dev/null | cut -d= -f2)
VAULT_SCRIPT := scripts/pi5_pull_vault.py

.PHONY: up down logs health vault-sync backup

## up: Build (if needed) and start the simulation container in detached mode
up:
	$(COMPOSE) up --build -d
	@echo ""
	@echo "Container started. Waiting for healthcheck..."
	@sleep 5
	@$(COMPOSE) ps $(CONTAINER)

## down: Stop and remove the simulation container
down:
	$(COMPOSE) down
	@echo "Container stopped."

## logs: Tail the simulation engine logs (Ctrl-C to exit)
logs:
	$(COMPOSE) logs -f --tail=100 $(CONTAINER)

## health: Check API liveness and SQLite DB integrity
health:
	@echo "=== API Health ==="
	@curl -sf $(API_URL) && echo " OK" || echo " FAIL — is the container running? (make up)"
	@echo ""
	@echo "=== SQLite DB ==="
	@if [ -f $(DB_PATH) ]; then \
		sqlite3 $(DB_PATH) "PRAGMA integrity_check; SELECT 'world_states: ' || COUNT(*) FROM world_states; SELECT 'characters: ' || COUNT(*) FROM characters; SELECT 'events: ' || COUNT(*) FROM events;" 2>/dev/null || echo "DB not initialised yet."; \
	else \
		echo "$(DB_PATH) not found — run 'make up' first."; \
	fi
	@echo ""
	@echo "=== Container Status ==="
	@$(COMPOSE) ps $(CONTAINER)

## vault-sync: Pull the latest Oracle export into /mnt/ssd/dreamcraft_vault/ (run on Pi 5)
vault-sync:
	@if [ -z "$(ORACLE_HOST)" ]; then \
		echo "ERROR: ORACLE_HOST not set. Export ORACLE_HOST=<ip> or add SAGA_ORACLE_HOST to .env"; \
		exit 1; \
	fi
	python3 $(VAULT_SCRIPT) \
		--oracle-user $(ORACLE_USER) \
		--oracle-host $(ORACLE_HOST)

## backup: Copy saga.db to ./backups/saga-<timestamp>.db
backup:
	@mkdir -p $(BACKUP_DIR)
	@if [ ! -f $(DB_PATH) ]; then \
		echo "ERROR: $(DB_PATH) not found. Nothing to back up."; \
		exit 1; \
	fi
	@STAMP=$$(date +%Y%m%d-%H%M%S); \
	cp $(DB_PATH) $(BACKUP_DIR)/saga-$$STAMP.db && \
	echo "Backup saved: $(BACKUP_DIR)/saga-$$STAMP.db ($$(du -sh $(BACKUP_DIR)/saga-$$STAMP.db | cut -f1))"
