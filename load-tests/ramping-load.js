import http from 'k6/http';
import { check, sleep } from 'k6';
import { getBaseUrl, recordResponseMetrics, formatCustomSummary } from './common.js';

const BASE_URL = getBaseUrl();

export const options = {
  scenarios: {
    ramping_stress_test: {
      executor: 'ramping-vus',
      startVUs: 1,
      stages: [
        { duration: '30s', target: 10 }, // Warmup
        { duration: '1m', target: 50 },  // Normal load
        { duration: '2m', target: 50 },  // Sustained load
        { duration: '1m', target: 100 }, // Stress peak
        { duration: '30s', target: 0 },  // Cooldown
      ],
    },
  },
  thresholds: {
    'http_5xx_total': ['count<20'],
    'http_req_duration': ['p(95)<1500'],
  },
};

export default function () {
  const page = (__ITER % 5) + 1;
  const res = http.get(`${BASE_URL}/api/products?page=${page}&pageSize=10`);
  recordResponseMetrics(res);

  check(res, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
  });

  sleep(0.05);
}

export function handleSummary(data) {
  return formatCustomSummary(data, 'Ramping Stress Test');
}
