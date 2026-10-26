import http from 'k6/http';
import { check, sleep } from 'k6';
import { getBaseUrl, recordResponseMetrics, formatCustomSummary } from './common.js';

const BASE_URL = getBaseUrl();
const ALLOW_WRITES = __ENV.ALLOW_WRITES === 'true';
const TEST_RUN_ID = __ENV.TEST_RUN_ID || `k6-run-${Date.now()}`;
const VUS = parseInt(__ENV.VUS || '5', 10);
const TEST_DURATION = __ENV.TEST_DURATION || '1m';

export const options = {
  scenarios: {
    db_read_write: {
      executor: 'constant-vus',
      vus: VUS,
      duration: TEST_DURATION,
    },
  },
  thresholds: {
    'http_5xx_total': ['count<10'],
    'http_req_duration': ['p(95)<800'],
  },
};

export function setup() {
  console.log(`[SETUP] Starting Database Read/Write Test Run: ${TEST_RUN_ID}`);
  console.log(`[SETUP] AllowWrites enabled: ${ALLOW_WRITES}`);
  return { testRunId: TEST_RUN_ID };
}

export default function (data) {
  // Read operation (80% ratio)
  const isRead = Math.random() < 0.8 || !ALLOW_WRITES;

  if (isRead) {
    const page = (__ITER % 3) + 1;
    const res = http.get(`${BASE_URL}/api/products?page=${page}&pageSize=5`);
    recordResponseMetrics(res);
    check(res, {
      'read status 200 or 429': (r) => r.status === 200 || r.status === 429,
    });
  } else {
    // Synthetic write operation (20% ratio when opt-in enabled)
    const payload = JSON.stringify({
      testRunId: data.testRunId,
      scenario: 'database-read-write',
      payload: `synthetic-data-vu-${__VU}-iter-${__ITER}`,
    });

    const headers = {
      'Content-Type': 'application/json',
      'X-Test-Run-Id': data.testRunId,
    };

    const res = http.post(`${BASE_URL}/api/test/db-write`, payload, { headers });
    recordResponseMetrics(res);
    check(res, {
      'write status 201 or 429': (r) => r.status === 201 || r.status === 429,
    });
  }

  sleep(0.05);
}

export function teardown(data) {
  if (ALLOW_WRITES) {
    console.log(`[TEARDOWN] Cleaning up test records for TestRunId: ${data.testRunId}...`);
    const delRes = http.del(`${BASE_URL}/api/test/runs/${data.testRunId}`);
    if (delRes.status === 200) {
      console.log(`[TEARDOWN] Scoped cleanup completed successfully: ${delRes.body}`);
    } else {
      console.warn(`[TEARDOWN] Warning: Cleanup returned status ${delRes.status}: ${delRes.body}`);
    }
  } else {
    console.log(`[TEARDOWN] Read-only test completed. No synthetic data written.`);
  }
}

export function handleSummary(data) {
  return formatCustomSummary(data, `Database Read/Write Test (Opt-in writes: ${ALLOW_WRITES})`);
}
