# Monolithic API Load Testing and Observability Lab

A complete, self-hosted environment to load-test an ASP.NET Core monolithic HTTP API, measure performance up to one million total requests, test rate limiting algorithms, and monitor the API and PostgreSQL under normal, sustained, spike, and stress traffic using OpenTelemetry, Prometheus, Grafana, Loki, and Tempo.

---

## 1. Architecture Overview

### Default Request Flow
```
k6 Load Generator ---> ASP.NET Core Web API (:8080) ---> PostgreSQL (:5432)
```

### Optional Proxy Request Flow
```
k6 Load Generator ---> Nginx Reverse Proxy (:8000) ---> ASP.NET Core Web API (:8080) ---> PostgreSQL (:5432)
```

### Observability Pipeline
```
ASP.NET Core API
  ├──> Scraped by Prometheus (:9090) on /metrics (HTTP request rates, latency histograms, in-flight gauges)
  └──> Emits OTLP (gRPC :4317) to OpenTelemetry Collector
         ├──> Traces forwarded to Grafana Tempo (:3200)
         ├──> Logs forwarded to Grafana Loki (:3100)
         └──> Metrics forwarded to Prometheus (:8889)

PostgreSQL
  └──> pg_stat metrics scraped by Prometheus via postgres-exporter (:9187)

Grafana (:3000)
  ├──> Correlates Prometheus metrics, Loki logs, and Tempo traces
  └──> Pre-provisioned dashboards with trace-to-log and log-to-trace navigation
```

---

## 2. Prerequisites & Quick Start

### Prerequisites
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or higher
- [Docker](https://docs.docker.com/get-docker/) & Docker Compose
- Optional: [Grafana k6 CLI](https://k6.io/docs/get-started/installation/) (or run k6 via Docker container)

### Quick Start

1. **Clone & Setup Environment File**:
   ```bash
   cp .env.example .env
   ```

2. **Start the Default Observability & API Stack**:
   - **PowerShell (Windows)**:
     ```powershell
     .\scripts\start.ps1
     ```
   - **Bash (Linux / macOS)**:
     ```bash
     ./scripts/start.sh
     ```

3. **Verify Health**:
   - Process Liveness: `curl http://localhost:8080/health/live`
   - Database Readiness: `curl http://localhost:8080/health/ready`
   - Aggregate Health: `curl http://localhost:8080/health`

4. **Access Web Interfaces**:
   - **API Swagger UI**: [http://localhost:8080/swagger](http://localhost:8080/swagger)
   - **Grafana**: [http://localhost:3000](http://localhost:3000) (User: `admin`, Password: `admin`)
   - **Prometheus UI**: [http://localhost:9090](http://localhost:9090)

5. **Run a Smoke Test**:
   ```bash
   # Using local k6:
   k6 run -e BASE_URL=http://localhost:8080 load-tests/smoke.js

   # Or using helper script (runs via Docker if k6 is not installed):
   .\scripts\run-load-test.ps1 -Scenario smoke
   ```

6. **Stop the Stack**:
   - **Preserve Data**:
     ```powershell
     .\scripts\stop.ps1
     ```
   - **Reset Data & Volumes**:
     ```powershell
     .\scripts\stop.ps1 -Reset
     ```

---

## 3. Configuration Reference

Settings are defined in `src/Api/appsettings.json` and can be overridden via environment variables using ASP.NET Core's double-underscore (`__`) convention.

| Setting | Environment Variable Override | Default | Purpose |
|---|---|---|---|
| `Application:Name` | `Application__Name` | `MonolithObservabilityLab` | Service identifier |
| `Application:Environment` | `ASPNETCORE_ENVIRONMENT` | `Development` | Hosting environment (`Development`, `Testing`, `Production`) |
| `Application:EnableSwagger`| `Application__EnableSwagger`| `true` | Enables Swagger UI documentation |
| `ConnectionStrings:Postgres` | `ConnectionStrings__Postgres` | `Host=postgres;...` | PostgreSQL connection string |
| `Database:ApplyMigrationsOnStartup`| `Database__ApplyMigrationsOnStartup` | `false` | Automatically applies EF migrations and seeds data |
| `Database:PoolMinSize` | `Database__PoolMinSize` | `0` | Minimum Npgsql connection pool size |
| `Database:PoolMaxSize` | `Database__PoolMaxSize` | `100` | Maximum Npgsql connection pool size |
| `Database:CommandTimeoutSeconds` | `Database__CommandTimeoutSeconds` | `30` | PostgreSQL command timeout |
| `RateLimiting:Enabled` | `RateLimiting__Enabled` | `true` | Enables/disables built-in rate limiting middleware |
| `RateLimiting:Policy` | `RateLimiting__Policy` | `FixedWindow` | Policy algorithm: `FixedWindow`, `SlidingWindow`, `TokenBucket`, `Concurrency` |
| `RateLimiting:PermitLimit` | `RateLimiting__PermitLimit` | `100` | Allowed request quota per window (Fixed/Sliding) |
| `RateLimiting:WindowSeconds` | `RateLimiting__WindowSeconds` | `60` | Time window duration in seconds |
| `RateLimiting:SegmentsPerWindow` | `RateLimiting__SegmentsPerWindow` | `6` | Window subdivisions for `SlidingWindow` |
| `RateLimiting:TokenLimit` | `RateLimiting__TokenLimit` | `100` | Maximum bucket capacity for `TokenBucket` |
| `RateLimiting:TokensPerPeriod` | `RateLimiting__TokensPerPeriod` | `100` | Refill token count for `TokenBucket` |
| `RateLimiting:ReplenishmentPeriodSeconds`| `RateLimiting__ReplenishmentPeriodSeconds`| `60` | Refill period in seconds for `TokenBucket` |
| `RateLimiting:ConcurrencyPermitLimit` | `RateLimiting__ConcurrencyPermitLimit` | `100` | Max concurrent in-flight requests for `Concurrency` |
| `RateLimiting:QueueLimit` | `RateLimiting__QueueLimit` | `0` | Number of requests queued when limit reached |
| `RateLimiting:IncludeRetryAfterHeader` | `RateLimiting__IncludeRetryAfterHeader`| `true` | Emits HTTP `Retry-After` header with 429 response |
| `RateLimiting:TrustedClientHeaderName` | `RateLimiting__TrustedClientHeaderName`| `X-Test-Client`| Header used to partition client quotas |
| `RateLimiting:TestHeaderAllowedOnlyInDevelopment`| `RateLimiting__TestHeaderAllowedOnlyInDevelopment`| `true` | Prevents untrusted header spoofing in Production |
| `TestEndpoints:Enabled` | `TestEndpoints__Enabled` | `false` | Exposes synthetic test endpoints (`/api/test/*`) |
| `TestEndpoints:AllowWrites`| `TestEndpoints__AllowWrites` | `false` | Explicit opt-in required for synthetic DB writes (rejected in Production) |
| `TestEndpoints:MaxSyntheticWritesPerRun`| `TestEndpoints__MaxSyntheticWritesPerRun`| `100000` | Maximum rows allowed per test run |
| `TestEndpoints:MaxRunDurationMinutes`| `TestEndpoints__MaxRunDurationMinutes` | `30` | Maximum age of an active test run |
| `TestEndpoints:RequireTestRunId`| `TestEndpoints__RequireTestRunId` | `true` | Enforces `X-Test-Run-Id` header on test writes |
| `Observability:OtlpEndpoint`| `Observability__OtlpEndpoint` | `http://otel-collector:4317` | OpenTelemetry Collector endpoint |
| `Observability:UseOtlpExporter`| `Observability__UseOtlpExporter` | `true` | Exports traces, metrics, logs via OTLP |
| `Observability:TraceSamplingRatio`| `Observability__TraceSamplingRatio`| `0.1` | Trace sampling ratio between `0.0` and `1.0` |
| `Observability:EnablePrometheusExporter`| `Observability__EnablePrometheusExporter`| `true` | Exposes `/metrics` Prometheus scrape endpoint |

---

## 4. Host vs. Compose Database Hostnames

- **Inside Docker Compose**: Services use Docker internal DNS. The database hostname is `postgres`:
  ```
  ConnectionStrings__Postgres=Host=postgres;Port=5432;Database=observability_lab;Username=app_user;Password=local_dev_only_change_me
  ```
- **Running API Directly on Host OS**: The container's port is published locally, so the database hostname is `localhost`:
  ```
  ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=observability_lab;Username=app_user;Password=local_dev_only_change_me
  ```

---

## 5. How to Change Rate Limits Without Code Edits

Rate limits are completely configuration-driven. To adjust policies or limits:

1. **Via `.env` file**:
   ```dotenv
   RateLimiting__Policy=SlidingWindow
   RateLimiting__PermitLimit=250
   RateLimiting__WindowSeconds=30
   RateLimiting__SegmentsPerWindow=6
   ```
2. **Restart the API container**:
   ```bash
   docker compose up -d api
   ```
3. **Verify in Health or Metrics**:
   Observe `rate_limit_accepted_total` and `rate_limit_rejected_total` in Prometheus or Grafana.

---

## 6. How to Enable Synthetic Writes Safely

Synthetic writes to `/api/test/db-write` allow generating database I/O load without modifying genuine business records (such as products and real customer orders).

### Safety Controls
1. **Disabled by Default**: Both `TestEndpoints:Enabled=true` AND `TestEndpoints:AllowWrites=true` are required.
2. **Production Block**: If the environment is `Production`, synthetic writes are hard-rejected with HTTP 403.
3. **Run Isolation**: Every synthetic record requires a `TEST_RUN_ID` header (`X-Test-Run-Id`).
4. **Scoped Cleanup**: At test completion, call:
   ```bash
   curl -X DELETE http://localhost:8080/api/test/runs/{testRunId}
   ```
   This deletes **only** the `load_test_records` associated with that run ID. It **never** drops databases, truncates tables, or alters product catalog data.

---

## 7. Optional Nginx Profile (Direct vs. Proxied Comparison)

The architecture supports comparing direct API load testing with testing through an Nginx reverse proxy.

### Direct API Testing (Default)
Traffic flows directly to the API:
```bash
# Start default stack
docker compose up -d

# Run test against direct API
k6 run -e BASE_URL=http://localhost:8080 load-tests/rate-limit.js
```

### Proxied Testing via Nginx
In proxied mode, Nginx listens on port 8000 and the direct API port is stripped from the host interface to prevent bypassing:
```bash
# Start with proxy override
docker compose -f docker-compose.yml -f docker-compose.proxy.yml up -d

# Run test through Nginx reverse proxy
k6 run -e BASE_URL=http://localhost:8000 load-tests/rate-limit.js
```

Nginx enforces an independent rate limiting zone (50 req/s, burst 20) with HTTP 429 status.

---

## 8. Grafana Dashboards

Grafana automatically provisions 5 comprehensive dashboards:

1. **Dashboard A: API Overview** (`/d/monolith-api-overview`)
   - Requests per second by status code.
   - p50, p95, p99 latency percentiles.
   - Status code distribution pie chart.
   - In-flight HTTP requests gauge.
   - Process CPU usage and GC heap memory.
2. **Dashboard B: Rate Limiter** (`/d/monolith-rate-limiter`)
   - Accepted vs Rejected requests/sec.
   - Rejection ratio percentage.
   - Total HTTP 429 count.
   - Latency under rejection pressure.
   - Rejections categorized by active policy.
3. **Dashboard C: PostgreSQL** (`/d/monolith-postgresql`)
   - Active vs Idle database connections.
   - Connection pool saturation vs configured maximum (100).
   - Transaction commit and rollback rates.
   - Deadlocks and conflicts.
   - Average database-backed query duration.
4. **Dashboard D: Load Testing** (`/d/monolith-load-testing`)
   - Overall throughput (RPS).
   - Response breakdown: 2xx Success vs 429 Rate Limited vs 5xx Server Error.
   - Cumulative completed requests counter.
   - p95 and p99 latency timeline.
   - Synthetic writes rate.
5. **Dashboard E: Logs and Traces** (`/d/monolith-logs-traces`)
   - Full-text application logs from Grafana Loki.
   - Trace search with min-duration filters in Grafana Tempo.
   - Clickable trace-to-log and log-to-trace navigation.
   - Step-by-step guidance for isolating slow PostgreSQL spans.

---

## 9. Investigating Slow Requests: Traces, Logs, and Metrics

When an alert triggers or p95 latency spikes:

1. **Identify the Time Window**: Open **Dashboard A** to note the exact spike time.
2. **Find the Slow Trace**:
   - Go to **Explore** in Grafana and choose datasource `Tempo`.
   - Set Service Name to `monolith-api` and Minimum Duration to `500ms`.
   - Select a high-latency trace to open the Trace View.
3. **Inspect the PostgreSQL Child Span**:
   - Locate the child span created by `Npgsql`.
   - Inspect tags: `db.statement`, `db.operation`, and `db.system`.
   - Determine whether query execution or connection acquisition caused the delay.
4. **Navigate to Correlated Logs**:
   - Click the document icon on the span. Grafana automatically navigates to Loki logs matching that exact `TraceId`.
   - Inspect contextual log messages and exception details.

---

## 10. Million-Request Test & Resource Implications

To run the full 1,000,000 request load test:

```bash
k6 run -e BASE_URL=http://localhost:8080 \
       -e TARGET_ITERATIONS=1000000 \
       -e VUS=50 \
       -e MAX_DURATION=30m \
       load-tests/fixed-total.js
```

### Important Considerations:
- **Same-Machine Testing**: Running k6, PostgreSQL, the API, and the observability stack on the same machine creates CPU and network contention. Measurements will reflect combined system capacity, not pure API throughput.
- **Port / Socket Exhaustion**: High iteration rates open and close thousands of TCP sockets. Ensure OS ephemeral port limits and `TIME_WAIT` reuse are enabled if socket errors occur.
- **Collector & Telemetry Ingestion**: A 1,000,000 request run generates heavy telemetry. The API uses a 10% trace sampling ratio (`TraceSamplingRatio: 0.1`) and batching in the OpenTelemetry Collector to prevent saturating disk I/O.
- **Iteration Target vs. Duration Limit**: `TARGET_ITERATIONS` is a target ceiling; if the API response times increase, `MAX_DURATION` acts as a safety cutoff.

---

## 11. Troubleshooting

| Issue | Root Cause | Solution |
|---|---|---|
| API readiness returns `503 Unhealthy` | Database unreachable or still starting | Check `docker compose ps` to ensure `postgres` is healthy. Verify `ConnectionStrings__Postgres` credentials. |
| Prometheus target `monolith-api` is Down | Network or scraping port mismatch | Verify container DNS name `api:8080`. Check that `/metrics` is accessible locally via `curl http://localhost:8080/metrics`. |
| Missing Traces in Tempo | OTLP endpoint unreachable or sampling ratio 0 | Verify `Observability__OtlpEndpoint=http://otel-collector:4317` and `Observability__TraceSamplingRatio` > 0. |
| `dropped_iterations` in k6 | k6 VU starvation in arrival-rate tests | Increase `PREALLOCATED_VUS` and `MAX_VUS` in `arrival-rate.js` so k6 has enough workers to generate the requested RPS. |
| Unexpected HTTP 429 during load test | API rate limiter capacity reached | Increase `RateLimiting__PermitLimit` or set `RateLimiting__Enabled=false` in `.env`. |
| Synthetic writes fail with HTTP 403 | Writes not explicitly opted-in | Set `TestEndpoints__Enabled=true` and `TestEndpoints__AllowWrites=true` in `.env`, and pass `-e ALLOW_WRITES=true` to k6. |
