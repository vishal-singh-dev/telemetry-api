import { Counter, Rate, Trend } from 'k6/metrics';

// Common Custom Metrics
export const count2xx = new Counter('http_2xx_total');
export const count429 = new Counter('http_429_total');
export const count4xx = new Counter('http_4xx_total');
export const count5xx = new Counter('http_5xx_total');
export const transportErrors = new Counter('http_transport_errors_total');
export const attemptedRequests = new Counter('http_attempted_requests_total');
export const responseTrend = new Trend('custom_req_duration_ms');

export function getBaseUrl() {
  const url = __ENV.BASE_URL;
  if (!url) {
    throw new Error('FATAL: BASE_URL environment variable is required. Example: BASE_URL=http://localhost:8080');
  }

  // Safety check: Reject production-looking targets unless explicitly authorized
  const allowProd = __ENV.ALLOW_PRODUCTION_TARGET === 'true';
  const lowerUrl = url.toLowerCase();
  if (!allowProd && (lowerUrl.includes('prod') || lowerUrl.includes('.com') || lowerUrl.includes('.io') || lowerUrl.includes('.org'))) {
    if (!lowerUrl.includes('localhost') && !lowerUrl.includes('127.0.0.1')) {
      throw new Error(`SAFETY REJECTION: Refusing to run load test against potential production target: '${url}'. Set ALLOW_PRODUCTION_TARGET=true to override.`);
    }
  }

  return url.replace(/\/+$/, '');
}

export function recordResponseMetrics(res) {
  attemptedRequests.add(1);
  if (!res || res.status === 0) {
    transportErrors.add(1);
    return;
  }

  responseTrend.add(res.timings.duration);

  if (res.status >= 200 && res.status < 300) {
    count2xx.add(1);
  } else if (res.status === 429) {
    count429.add(1);
  } else if (res.status >= 400 && res.status < 500) {
    count4xx.add(1);
  } else if (res.status >= 500) {
    count5xx.add(1);
  }
}

export function formatCustomSummary(data, title) {
  const getMetric = (name, fallback = 0) => {
    return data.metrics[name]?.values?.count || fallback;
  };

  const p50 = data.metrics['http_req_duration']?.values['p(50)']?.toFixed(2) || 'N/A';
  const p90 = data.metrics['http_req_duration']?.values['p(90)']?.toFixed(2) || 'N/A';
  const p95 = data.metrics['http_req_duration']?.values['p(95)']?.toFixed(2) || 'N/A';
  const p99 = data.metrics['http_req_duration']?.values['p(99)']?.toFixed(2) || 'N/A';

  const totalReqs = data.metrics['http_reqs']?.values?.count || 0;
  const c2xx = getMetric('http_2xx_total');
  const c429 = getMetric('http_429_total');
  const c4xx = getMetric('http_4xx_total');
  const c5xx = getMetric('http_5xx_total');
  const tErr = getMetric('http_transport_errors_total');
  const droppedIters = data.metrics['dropped_iterations']?.values?.count || 0;

  const output = `
================================================================================
  k6 Load Test Execution Summary: ${title}
================================================================================
  Total HTTP Requests Attempted: ${totalReqs}
  - 2xx Success:                 ${c2xx}
  - 429 Rate Limited:            ${c429}
  - Other 4xx Errors:            ${c4xx}
  - 5xx Server Errors:           ${c5xx}
  - Network / Transport Errors:  ${tErr}
  - Dropped Iterations:          ${droppedIters}

  Latency Percentiles (ms):
  - p50 (Median):                ${p50} ms
  - p90:                         ${p90} ms
  - p95:                         ${p95} ms
  - p99:                         ${p99} ms
================================================================================
`;
  return {
    stdout: output,
  };
}
