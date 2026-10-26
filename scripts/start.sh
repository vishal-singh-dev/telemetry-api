#!/usr/bin/env bash
set -euo pipefail

PROXY_MODE=false
for arg in "$@"; do
  if [ "$arg" == "--proxy" ]; then
    PROXY_MODE=true
  fi
done

if [ ! -f ".env" ]; then
  echo "Creating .env from .env.example..."
  cp .env.example .env
fi

if [ "$PROXY_MODE" = true ]; then
  echo "Starting Monolith Observability Lab with Nginx Reverse Proxy..."
  docker compose -f docker-compose.yml -f docker-compose.proxy.yml up -d --build
  echo "Stack started! Nginx Reverse Proxy available at: http://localhost:8000"
else
  echo "Starting Monolith Observability Lab in Direct API Mode..."
  docker compose up -d --build
  echo "Stack started! Direct API available at: http://localhost:8080"
fi

echo "Grafana:    http://localhost:3000 (admin / admin)"
echo "Prometheus: http://localhost:9090"
echo "Swagger UI: http://localhost:8080/swagger"
