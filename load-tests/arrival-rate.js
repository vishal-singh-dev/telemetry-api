import http from 'k6/http';
import { check } from 'k6';
import { getBaseUrl, recordResponseMetrics, formatCustomSummary } from './common.js';

const BASE_URL = getBaseUrl();
const TARGET_RPS = parseInt(__ENV.TARGET_RPS || '100', 10);
const TEST_DURATION = __ENV.TEST_DURATION || '1m';
const PREALLOCATED_VUS = parseInt(__ENV.PREALLOCATED_VUS || '20', 10);
const MAX_VUS = parseInt(__ENV.MAX_VUS || '100', 10);

export const options = {
  scenarios: {
    constant_rate: {
      executor: 'constant-arrival-rate',
      rate: TARGET_RPS,
      timeUnit: '1s',
      duration: TEST_DURATION,
      preAllocatedVUs: PREALLOCATED_VUS,
      maxVUs: MAX_VUS,
    },
  },
  thresholds: {
    'http_req_failed': ['rate<0.05'],
    'http_req_duration': ['p(95)<500'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/api/products?page=1&pageSize=10`);
  recordResponseMetrics(res);

  check(res, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
  });
}

export function handleSummary(data) {
  const summary = formatCustomSummary(data, `Constant Arrival Rate (${TARGET_RPS} RPS target)`);
  const droppedIters = data.metrics['dropped_iterations']?.values?.count || 0;
  if (droppedIters > 0) {
    summary.stdout += `\n  [WARNING]: ${droppedIters} iteration(s) were dropped by k6. The generator was VU-constrained.\n  Consider increasing PREALLOCATED_VUS or MAX_VUS before judging API capacity.\n`;
  }
  return summary;
}
