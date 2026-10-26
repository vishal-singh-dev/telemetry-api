#!/usr/bin/env bash
set -euo pipefail

RESET_MODE=false
for arg in "$@"; do
  if [ "$arg" == "--reset" ]; then
    RESET_MODE=true
  fi
done

if [ "$RESET_MODE" = true ]; then
  echo "WARNING: This command will permanently delete all Docker volumes (database data, Prometheus metrics, Loki logs, Tempo traces)!"
  read -p "Are you sure you want to proceed? (y/N): " -r confirm
  if [[ "$confirm" =~ ^[Yy]$ ]]; then
    echo "Stopping stack and removing volumes..."
    docker compose -f docker-compose.yml -f docker-compose.proxy.yml down -v --remove-orphans
    echo "Stack stopped and persistent data reset."
  else
    echo "Reset aborted."
  fi
else
  echo "Stopping stack (preserving persistent data)..."
  docker compose -f docker-compose.yml -f docker-compose.proxy.yml down --remove-orphans
  echo "Stack stopped."
fi
