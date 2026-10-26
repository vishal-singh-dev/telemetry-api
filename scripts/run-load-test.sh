#!/usr/bin/env bash
set -euo pipefail

SCENARIO="${1:-smoke}"
BASE_URL="${2:-http://localhost:8080}"
EXTRA_ARGS="${@:3}"

SCRIPT_PATH="load-tests/${SCENARIO}.js"
if [ ! -f "$SCRIPT_PATH" ]; then
  echo "Error: Scenario script '$SCRIPT_PATH' does not exist."
  exit 1
fi

if command -v k6 &> /dev/null; then
  echo "Running $SCENARIO test using local k6 CLI against $BASE_URL..."
  k6 run -e BASE_URL="$BASE_URL" $EXTRA_ARGS "$SCRIPT_PATH"
else
  echo "Local k6 CLI not found. Running $SCENARIO test via Docker container..."
  docker run --rm -i \
    --network host \
    -v "${PWD}/load-tests:/load-tests:ro" \
    grafana/k6:0.50.0 run \
    -e BASE_URL="$BASE_URL" $EXTRA_ARGS \
    "/load-tests/${SCENARIO}.js"
fi
