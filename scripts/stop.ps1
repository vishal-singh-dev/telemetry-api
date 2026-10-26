param (
    [switch]$Reset
)

$ErrorActionPreference = "Stop"

if ($Reset) {
    Write-Warning "WARNING: This command will permanently delete all Docker volumes (database data, Prometheus metrics, Loki logs, Tempo traces)!"
    $confirm = Read-Host "Are you sure you want to proceed? (y/N)"
    if ($confirm -eq "y" -or $confirm -eq "Y") {
        Write-Host "Stopping stack and removing volumes..." -ForegroundColor Yellow
        docker compose -f docker-compose.yml -f docker-compose.proxy.yml down -v --remove-orphans
        Write-Host "Stack stopped and persistent data reset." -ForegroundColor Green
    } else {
        Write-Host "Reset aborted." -ForegroundColor Cyan
    }
} else {
    Write-Host "Stopping stack (preserving persistent data)..." -ForegroundColor Cyan
    docker compose -f docker-compose.yml -f docker-compose.proxy.yml down --remove-orphans
    Write-Host "Stack stopped." -ForegroundColor Green
}
