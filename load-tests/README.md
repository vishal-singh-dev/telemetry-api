# k6 Load Testing Suite

This directory contains Grafana k6 performance scenarios designed to evaluate the monolithic ASP.NET Core API under baseline, sustained, spike, and rate-limited conditions.

---

## 1. Test Scenarios Overview

| Script | Objective | Executor | Default Profile |
|---|---|---|---|
| `smoke.js` | Validate system health, DB reads, and telemetry | `per-vu-iterations` | 2 VUs, 30 iterations |
| `fixed-total.js` | Stress test up to 1,000,000 requests | `shared-iterations` | 50 VUs, 1,000,000 iters |
| `rate-limit.js` | Validate 429 status and client partition isolation | `constant-vus` | 10 VUs, 5 clients, 1m |
| `arrival-rate.js` | Open-model constant arrival rate test | `constant-arrival-rate` | 100 RPS, 1m |
| `ramping-load.js` | Staged ramp-up, plateau, stress peak, cooldown | `ramping-vus` | 0 -> 100 VUs, 5m total |
| `spike.js` | Sudden traffic spike from 5 to 150 VUs and recovery | `ramping-vus` | 5 -> 150 -> 5 VUs, 2m |
| `database-read-write.js` | Mixed 80/20 read/write with scoped teardown | `constant-vus` | 5 VUs, 1m |

---

## 2. Environment Variables Reference

| Variable | Default | Purpose |
|---|---|---|
| `BASE_URL` | *Required* | Base URL of API (`http://localhost:8080` or `http://localhost:8000` via Nginx) |
| `TARGET_ITERATIONS` | `1000000` (fixed-total), `30` (smoke) | Target total iterations across all VUs |
| `VUS` | Scenario dependent | Number of concurrent Virtual Users |
| `MAX_DURATION` | `30m` | Hard cutoff duration for iteration-based tests |
| `TARGET_RPS` | `100` | Target arrivals per second for `arrival-rate.js` |
| `TEST_DURATION` | `1m` | Test execution duration for time-based scenarios |
| `CLIENT_COUNT` | `5` | Number of distinct client partitions for rate limiting |
| `CLIENT_PREFIX` | `k6-client-` | Prefix for `X-Test-Client` header |
| `ALLOW_WRITES` | `false` | Explicit opt-in flag to allow synthetic writes in DB test |
| `TEST_RUN_ID` | Auto-generated | Unique identifier for the test run for scoped cleanup |
| `ALLOW_PRODUCTION_TARGET`| `false` | Safety override to prevent testing against non-local domains |

---

## 3. Running Load Tests

### Option A: Using Local k6 CLI

```bash
# Smoke Test
k6 run -e BASE_URL=http://localhost:8080 load-tests/smoke.js

# Rate Limiting Test (5 client partitions)
k6 run -e BASE_URL=http://localhost:8080 -e CLIENT_COUNT=5 -e TEST_DURATION=1m load-tests/rate-limit.js

# Constant Arrival Rate (200 RPS)
k6 run -e BASE_URL=http://localhost:8080 -e TARGET_RPS=200 load-tests/arrival-rate.js

# Ramping Load / Stress Test
k6 run -e BASE_URL=http://localhost:8080 load-tests/ramping-load.js

# Traffic Spike Test
k6 run -e BASE_URL=http://localhost:8080 load-tests/spike.js

# Database Read & Write Test (Opt-in writes)
k6 run -e BASE_URL=http://localhost:8080 -e ALLOW_WRITES=true load-tests/database-read-write.js

# Million Request Run (configurable iterations)
k6 run -e BASE_URL=http://localhost:8080 -e TARGET_ITERATIONS=1000000 -e VUS=100 -e MAX_DURATION=30m load-tests/fixed-total.js
```

### Option B: Running via Docker One-Off Container

If `k6` is not installed locally on your host machine:

```bash
# On Linux / macOS / PowerShell:
docker run --rm -i \
  --network host \
  -v "${PWD}/load-tests:/load-tests:ro" \
  grafana/k6:0.50.0 run \
  -e BASE_URL=http://localhost:8080 \
  /load-tests/smoke.js
```

Or using the internal Docker Compose network:
```bash
docker run --rm -i \
  --network telemetry_observability-net \
  -v "${PWD}/load-tests:/load-tests:ro" \
  grafana/k6:0.50.0 run \
  -e BASE_URL=http://api:8080 \
  /load-tests/smoke.js
```

---

## 4. Result Metrics and Interpretation

The custom summary output groups outcomes into distinct categories:

- **2xx Success**: Successfully serviced HTTP requests within capacity.
- **429 Rate Limited**: Throttled requests that were rejected before reaching database handlers.
- **5xx Server Errors**: Internal API failures or unhandled exceptions (indicates bug or saturation).
- **Transport Errors**: Socket errors, TCP connection resets, or gateway timeouts.
- **Dropped Iterations**: In `arrival-rate.js`, indicates k6 itself ran out of allocated VUs to sustain the requested arrival rate.

---

## 5. Safety Precautions

1. **Production Guard**: All scripts reject URLs with production-like domains unless `ALLOW_PRODUCTION_TARGET=true` is set.
2. **Opt-in Synthetic Writes**: Synthetic write tests (`/api/test/db-write`) require both `TestEndpoints:AllowWrites=true` in the API and `-e ALLOW_WRITES=true` in k6.
3. **Run-Scoped Cleanup**: Synthetic writes tag records with a unique `TEST_RUN_ID`. At test completion, `teardown()` issues a `DELETE /api/test/runs/{TEST_RUN_ID}` call, ensuring catalog data is never touched and table truncations never occur.
