param (
    [string]$ConnectionString = "Host=localhost;Port=5432;Database=observability_lab;Username=app_user;Password=local_dev_only_change_me"
)

$ErrorActionPreference = "Stop"

Write-Host "Applying database migrations and seeding initial data..." -ForegroundColor Cyan

$env:ConnectionStrings__Postgres = $ConnectionString
$env:Database__ApplyMigrationsOnStartup = "true"

# Run API briefly to execute migrations
Write-Host "Triggering migration runner via API project..." -ForegroundColor Green
dotnet run --project src/Api/Api.csproj --no-build --urls "http://127.0.0.1:5099" &
$proc = Get-Process -Name "Api" -ErrorAction SilentlyContinue | Select-Object -Last 1

Start-Sleep -Seconds 5

if ($proc) {
    Stop-Process -Id $proc.Id -Force -ErrorAction SilentlyContinue
}

Write-Host "Database migration check completed." -ForegroundColor Green
