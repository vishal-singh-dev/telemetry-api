import http from 'k6/http';
import { check, sleep } from 'k6';
import { getBaseUrl, recordResponseMetrics, formatCustomSummary } from './common.js';

const BASE_URL = getBaseUrl();
const CLIENT_COUNT = parseInt(__ENV.CLIENT_COUNT || '5', 10);
const CLIENT_PREFIX = __ENV.CLIENT_PREFIX || 'k6-client-';
const VUS = parseInt(__ENV.VUS || '10', 10);
const TEST_DURATION = __ENV.TEST_DURATION || '1m';

export const options = {
  scenarios: {
    rate_limit_partition_test: {
      executor: 'constant-vus',
      vus: VUS,
      duration: TEST_DURATION,
    },
  },
  thresholds: {
    // 429 is an expected outcome during rate-limit testing, so 5xx is the failure metric
    'http_5xx_total': ['count<5'],
    'http_transport_errors_total': ['count<5'],
  },
};

export default function () {
  // Rotate client key across configured client partitions
  const clientIndex = (__VU + __ITER) % CLIENT_COUNT;
  const clientId = `${CLIENT_PREFIX}${clientIndex}`;

  const res = http.get(`${BASE_URL}/api/products/1`, {
    headers: {
      'X-Test-Client': clientId,
      'Accept': 'application/json',
    },
  });

  recordResponseMetrics(res);

  check(res, {
    'expected status (200 or 429)': (r) => r.status === 200 || r.status === 429,
    'retry-after header present on 429': (r) => r.status !== 429 || r.headers['Retry-After'] !== undefined,
  });

  // Short pause to allow burst observation
  sleep(0.05);
}

export function handleSummary(data) {
  return formatCustomSummary(data, `Rate Limiting Test (${CLIENT_COUNT} Independent Client Partitions)`);
}
