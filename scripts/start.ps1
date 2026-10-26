param (
    [switch]$Proxy
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path ".env")) {
    Write-Host "Creating .env from .env.example..." -ForegroundColor Cyan
    Copy-Item ".env.example" ".env"
}

if ($Proxy) {
    Write-Host "Starting Monolith Observability Lab with Nginx Reverse Proxy..." -ForegroundColor Green
    docker compose -f docker-compose.yml -f docker-compose.proxy.yml up -d --build
    Write-Host "Stack started! Nginx Reverse Proxy available at: http://localhost:8000" -ForegroundColor Green
} else {
    Write-Host "Starting Monolith Observability Lab in Direct API Mode..." -ForegroundColor Green
    docker compose up -d --build
    Write-Host "Stack started! Direct API available at: http://localhost:8080" -ForegroundColor Green
}

Write-Host "Grafana:    http://localhost:3000 (admin / admin)" -ForegroundColor Cyan
Write-Host "Prometheus: http://localhost:9090" -ForegroundColor Cyan
Write-Host "Swagger UI: http://localhost:8080/swagger" -ForegroundColor Cyan
