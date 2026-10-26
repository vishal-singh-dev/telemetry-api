import http from 'k6/http';
import { check, sleep } from 'k6';
import { getBaseUrl, recordResponseMetrics, formatCustomSummary } from './common.js';

const BASE_URL = getBaseUrl();

export const options = {
  scenarios: {
    spike_test: {
      executor: 'ramping-vus',
      startVUs: 2,
      stages: [
        { duration: '30s', target: 5 },   // Baseline normal traffic
        { duration: '10s', target: 150 }, // Rapid sudden traffic spike
        { duration: '30s', target: 150 }, // Hold peak briefly
        { duration: '10s', target: 5 },   // Rapid drop back to baseline
        { duration: '30s', target: 5 },   // Observe system recovery
      ],
    },
  },
  thresholds: {
    // Allow for rate limiting during spike, but ensure server remains resilient without crashing
    'http_5xx_total': ['count<50'],
  },
};

export default function () {
  const res = http.get(`${BASE_URL}/api/products/1`);
  recordResponseMetrics(res);

  check(res, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
  });

  sleep(0.02);
}

export function handleSummary(data) {
  return formatCustomSummary(data, 'Spike Traffic Test');
}
