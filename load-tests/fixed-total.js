import http from 'k6/http';
import { check } from 'k6';
import { getBaseUrl, recordResponseMetrics, formatCustomSummary } from './common.js';

const BASE_URL = getBaseUrl();
const TARGET_ITERATIONS = parseInt(__ENV.TARGET_ITERATIONS || '1000000', 10);
const VUS = parseInt(__ENV.VUS || '50', 10);
const MAX_DURATION = __ENV.MAX_DURATION || '30m';

export const options = {
  scenarios: {
    fixed_total_run: {
      executor: 'shared-iterations',
      vus: VUS,
      iterations: TARGET_ITERATIONS,
      maxDuration: MAX_DURATION,
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.05'], // <5% transport or 5xx failures
    http_req_duration: ['p(95)<1000'], // 95% within 1s under high throughput
  },
};

// Documented: Exactly 1 request per iteration to maintain 1:1 iteration-to-request ratio
export default function () {
  // Query product catalog page
  const res = http.get(`${BASE_URL}/api/products?page=1&pageSize=10`, {
    headers: {
      'Accept': 'application/json',
    },
  });

  recordResponseMetrics(res);

  check(res, {
    'status is 200 or 429': (r) => r.status === 200 || r.status === 429,
  });
}

export function handleSummary(data) {
  return formatCustomSummary(data, `Fixed Total Target Run (${TARGET_ITERATIONS.toLocaleString()} target iterations)`);
}
