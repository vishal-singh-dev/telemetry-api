#!/usr/bin/env bash
set -euo pipefail

CONNECTION_STRING="${1:-Host=localhost;Port=5432;Database=observability_lab;Username=app_user;Password=local_dev_only_change_me}"

echo "Applying database migrations and seeding initial data..."
export ConnectionStrings__Postgres="$CONNECTION_STRING"
export Database__ApplyMigrationsOnStartup="true"

dotnet run --project src/Api/Api.csproj --no-build --urls "http://127.0.0.1:5099" &
API_PID=$!

sleep 5

kill "$API_PID" || true
echo "Database migration check completed."
