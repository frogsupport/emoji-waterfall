// k6 load test for Emoji Waterfall.
// Run it with the command in deploy/README.md (section "Load test").
import http from 'k6/http';
import { check } from 'k6';

// The target comes from the BASE_URL environment variable.
// There is no default, so a typo can never hit production by accident.
const RAW_BASE_URL = __ENV.BASE_URL;
if (!RAW_BASE_URL) {
  throw new Error('Set BASE_URL, for example -e BASE_URL=https://emojiwaterfall.com');
}
// Remove trailing slashes, so the request path never has a double slash.
const BASE_URL = RAW_BASE_URL.replace(/\/+$/, '');

export const options = {
  vus: 20,        // 20 virtual users, each one loops the function below
  duration: '60s',
  thresholds: {
    http_req_failed: ['rate<0.01'],     // under 1% of requests fail
    http_req_duration: ['p(95)<500'],   // 95% of requests finish within 500 ms
    checks: ['rate>0.99'],              // over 99% of checks pass (a failed check alone does not fail the run)
  },
};

// Each virtual user runs this function again and again.
export default function () {
  const res = http.get(`${BASE_URL}/api/emojis?count=50`);

  // A check records pass or fail. It does not stop the test.
  check(res, {
    'status is 200': (r) => r.status === 200,
    'body is a JSON array of 50 items': (r) => {
      try {
        const body = r.json();
        return Array.isArray(body) && body.length === 50;
      } catch (e) {
        return false;
      }
    },
  });
}
