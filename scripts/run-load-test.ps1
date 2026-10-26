param (
    [Parameter(Mandatory = $true)]
    [ValidateSet("smoke", "fixed-total", "rate-limit", "arrival-rate", "ramping-load", "spike", "database-read-write")]
    [string]$Scenario,

    [string]$BaseUrl = "http://localhost:8080",
    [string]$ExtraArgs = ""
)

$ErrorActionPreference = "Stop"

$scriptPath = "load-tests/$Scenario.js"
if (-not (Test-Path $scriptPath)) {
    Write-Error "Load test script not found: $scriptPath"
}

# Check if k6 is available locally
$k6Cmd = Get-Command "k6" -ErrorAction SilentlyContinue

if ($k6Cmd) {
    Write-Host "Running $Scenario test using local k6 CLI against $BaseUrl..." -ForegroundColor Green
    Invoke-Expression "k6 run -e BASE_URL=$BaseUrl $ExtraArgs $scriptPath"
} else {
    Write-Host "Local k6 CLI not found. Running $Scenario test via Docker container against $BaseUrl..." -ForegroundColor Yellow
    # On Windows host, host.docker.internal reaches the host services
    $dockerTargetUrl = $BaseUrl.Replace("localhost", "host.docker.internal").Replace("127.0.0.1", "host.docker.internal")
    docker run --rm -i -v "${PWD}/load-tests:/load-tests:ro" grafana/k6:0.50.0 run -e BASE_URL=$dockerTargetUrl $ExtraArgs "/load-tests/$Scenario.js"
}
