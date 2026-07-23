// k6 Smoke Test
// Quick sanity check that the API is working under load
/* global __ENV */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate, Trend } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const healthCheckDuration = new Trend('health_check_duration');

// Only count 5xx as http_req_failed (4xx like 404 is expected in CI with empty DB)
http.setResponseCallback(http.expectedStatuses({ min: 200, max: 499 }));

// Test configuration - VUS and DURATION can be overridden via K6_VUS / K6_DURATION env vars
export const options = {
  vus: Number.parseInt(__ENV.K6_VUS) || 10,
  duration: __ENV.K6_DURATION || '30s',
  thresholds: {
    http_req_duration: ['p(95)<500'],
    errors: ['rate<0.1'],       // Custom error rate (5xx only) below 10%
    http_req_failed: ['rate<0.05'], // Server errors (5xx) below 5%
  },
};

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5000';

export default function () {
  // Health check endpoint
  const healthRes = http.get(`${BASE_URL}/health`);
  healthCheckDuration.add(healthRes.timings.duration);

  const healthOk = check(healthRes, {
    'health check status is 200': (r) => r.status === 200,
    'health check response time < 200ms': (r) => r.timings.duration < 200,
  });
  if (!healthOk && healthRes.status >= 500) {
    errorRate.add(1);
  }

  // API info endpoint (if exists)
  const infoRes = http.get(`${BASE_URL}/api/info`, {
    headers: { 'Content-Type': 'application/json' },
  });

  check(infoRes, {
    'info endpoint responds': (r) => r.status === 200 || r.status === 404,
  });

  // Projects list endpoint
  const projectsRes = http.get(`${BASE_URL}/api/core/projects`, {
    headers: { 'Content-Type': 'application/json' },
  });

  const projectsOk = check(projectsRes, {
    'projects endpoint responds': (r) => r.status === 200 || r.status === 401 || r.status === 404,
    'projects response time < 500ms': (r) => r.timings.duration < 500,
  });
  if (!projectsOk && projectsRes.status >= 500) {
    errorRate.add(1);
  }

  sleep(1);
}

export function handleSummary(data) {
  return {
    'tests/performance/results/smoke-test-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: ' ', enableColors: true }),
  };
}

function textSummary(data, opts) {
  const indent = opts.indent || '';
  let summary = '\n';
  summary += `${indent}=== SMOKE TEST SUMMARY ===\n\n`;

  if (data.metrics) {
    summary += `${indent}HTTP Requests:\n`;
    summary += `${indent}  Total: ${data.metrics.http_reqs?.values?.count || 0}\n`;
    summary += `${indent}  Failed (5xx): ${((data.metrics.http_req_failed?.values?.rate || 0) * 100).toFixed(2)}%\n`;
    summary += `${indent}  Avg Duration: ${data.metrics.http_req_duration?.values?.avg?.toFixed(2) || 0}ms\n`;
    summary += `${indent}  P95 Duration: ${data.metrics.http_req_duration?.values['p(95)']?.toFixed(2) || 0}ms\n`;
  }

  return summary;
}
