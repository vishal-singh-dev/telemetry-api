import http from 'k6/http';
import { check, sleep } from 'k6';
import { getBaseUrl, recordResponseMetrics, formatCustomSummary } from './common.js';

const BASE_URL = getBaseUrl();
const VUS = parseInt(__ENV.VUS || '2', 10);
const TARGET_ITERATIONS = parseInt(__ENV.TARGET_ITERATIONS || '30', 10);

export const options = {
  scenarios: {
    smoke_test: {
      executor: 'per-vu-iterations',
      vus: VUS,
      iterations: Math.floor(TARGET_ITERATIONS / VUS),
      maxDuration: '1m',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'], // <1% failures
    http_req_duration: ['p(95)<300'], // 95% under 300ms
  },
};

export default function () {
  // 1. Health check liveness
  const liveRes = http.get(`${BASE_URL}/health/live`);
  recordResponseMetrics(liveRes);
  check(liveRes, {
    'liveness status 200': (r) => r.status === 200,
    'liveness payload contains Live': (r) => r.json('status') === 'Live',
  });

  // 2. Health check readiness (PostgreSQL connectivity)
  const readyRes = http.get(`${BASE_URL}/health/ready`);
  recordResponseMetrics(readyRes);
  check(readyRes, {
    'readiness status 200': (r) => r.status === 200,
    'readiness payload confirms Healthy DB': (r) => r.json('database') === 'Healthy',
  });

  // 3. Database-backed product listing
  const prodRes = http.get(`${BASE_URL}/api/products?page=1&pageSize=5`);
  recordResponseMetrics(prodRes);
  check(prodRes, {
    'products status 200': (r) => r.status === 200,
    'products has items': (r) => r.json('items') && r.json('items').length > 0,
  });

  // 4. Single product detail fetch
  const singleRes = http.get(`${BASE_URL}/api/products/1`);
  recordResponseMetrics(singleRes);
  check(singleRes, {
    'product 1 fetch status 200': (r) => r.status === 200,
    'product 1 has price': (r) => r.json('price') !== undefined,
  });

  sleep(0.1);
}

export function handleSummary(data) {
  return formatCustomSummary(data, 'Smoke Test');
}
