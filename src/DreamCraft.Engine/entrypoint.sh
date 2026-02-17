#!/bin/sh
set -eu

DB_PATH="${SAGA_DB_PATH:-/app/data/saga.db}"
API_PORT="${SAGA_API_PORT:-8002}"

python -m dreamcraft_v2.data.setup_db --db-path "$DB_PATH"
exec uvicorn dreamcraft_v2.api.main:app --host 0.0.0.0 --port "$API_PORT"
