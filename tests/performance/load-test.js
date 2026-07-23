// k6 Load Test
// Simulates realistic load on the API
/* global __ENV */

import http from 'k6/http';
import { check, sleep, group } from 'k6';
import { Rate, Trend, Counter } from 'k6/metrics';

// Custom metrics
const errorRate = new Rate('errors');
const apiCalls = new Counter('api_calls');
const projectsResponseTime = new Trend('projects_response_time');
const authResponseTime = new Trend('auth_response_time');

// Test configuration
export const options = {
  stages: [
    { duration: '30s', target: parseInt(__ENV.K6_VUS) || 10 },  // Ramp up
    { duration: __ENV.K6_DURATION || '1m', target: parseInt(__ENV.K6_VUS) || 10 }, // Stay at peak
    { duration: '30s', target: 0 },  // Ramp down
  ],
  thresholds: {
    http_req_duration: ['p(95)<1000', 'p(99)<2000'], // Response time thresholds
    errors: ['rate<0.05'], // Error rate below 5%
    http_req_failed: ['rate<0.05'],
  },
};

const BASE_URL = __ENV.K6_BASE_URL || 'http://localhost:5000';

// Simulated user scenarios
export default function () {
  group('Health Check', () => {
    const res = http.get(`${BASE_URL}/health`);
    check(res, {
      'health check OK': (r) => r.status === 200,
    }) || errorRate.add(1);
    apiCalls.add(1);
  });

  group('Browse Projects', () => {
    // List projects
    const listRes = http.get(`${BASE_URL}/api/core/projects?page=1&limit=10`, {
      headers: { 'Content-Type': 'application/json' },
    });
    projectsResponseTime.add(listRes.timings.duration);

    check(listRes, {
      'projects list status': (r) => r.status === 200 || r.status === 401 || r.status === 404,
      'projects list response time': (r) => r.timings.duration < 500,
    }) || errorRate.add(1);
    apiCalls.add(1);

    // Search projects
    const searchRes = http.get(`${BASE_URL}/api/core/projects/search?q=test`, {
      headers: { 'Content-Type': 'application/json' },
    });

    check(searchRes, {
      'search responds': (r) => r.status === 200 || r.status === 404,
    });
    apiCalls.add(1);

    sleep(1);
  });

  group('User Flow', () => {
    // Simulate viewing user profile (public endpoint)
    const userRes = http.get(`${BASE_URL}/api/core/users/1`, {
      headers: { 'Content-Type': 'application/json' },
    });
    
    check(userRes, {
      'user endpoint responds': (r) => r.status === 200 || r.status === 404 || r.status === 401,
    });
    apiCalls.add(1);

    sleep(0.5);
  });

  group('Skills & Tech Stack', () => {
    // Get skills list
    const skillsRes = http.get(`${BASE_URL}/api/core/skills`, {
      headers: { 'Content-Type': 'application/json' },
    });
    
    check(skillsRes, {
      'skills endpoint responds': (r) => r.status === 200 || r.status === 404,
    });
    apiCalls.add(1);

    sleep(0.5);
  });

  // Random think time between iterations
  sleep(Math.random() * 2 + 1);
}

export function handleSummary(data) {
  const summary = {
    timestamp: new Date().toISOString(),
    config: {
      vus: __ENV.K6_VUS || 10,
      duration: __ENV.K6_DURATION || '1m',
      baseUrl: BASE_URL,
    },
    results: data,
  };

  return {
    'tests/performance/results/load-test-summary.json': JSON.stringify(summary, null, 2),
    stdout: generateTextSummary(data),
  };
}

function generateTextSummary(data) {
  let summary = '\n';
  summary += '╔══════════════════════════════════════════════════════════════╗\n';
  summary += '║                   LOAD TEST SUMMARY                          ║\n';
  summary += '╚══════════════════════════════════════════════════════════════╝\n\n';
  
  if (data.metrics) {
    const reqs = data.metrics.http_reqs?.values?.count || 0;
    const failed = data.metrics.http_req_failed?.values?.rate || 0;
    const avgDuration = data.metrics.http_req_duration?.values?.avg || 0;
    const p95Duration = data.metrics.http_req_duration?.values['p(95)'] || 0;
    const p99Duration = data.metrics.http_req_duration?.values['p(99)'] || 0;
    
    summary += `📊 HTTP Requests\n`;
    summary += `   Total Requests: ${reqs}\n`;
    summary += `   Failed Rate: ${(failed * 100).toFixed(2)}%\n\n`;
    
    summary += `⏱️  Response Times\n`;
    summary += `   Average: ${avgDuration.toFixed(2)}ms\n`;
    summary += `   P95: ${p95Duration.toFixed(2)}ms\n`;
    summary += `   P99: ${p99Duration.toFixed(2)}ms\n\n`;
    
    // Threshold results
    summary += `✅ Thresholds\n`;
    for (const [name, threshold] of Object.entries(data.thresholds || {})) {
      const status = threshold.ok ? '✓' : '✗';
      summary += `   ${status} ${name}\n`;
    }
  }
  
  return summary;
}
