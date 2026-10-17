// Load test for POST /api/v1/applications. Run it with `make load` while the stack is up.
// `make load` usually restarts the API, so the test waits for it, warms it up, and only then measures.
import http from "k6/http";
import { check, fail, sleep } from "k6";

export const options = {
  scenarios: {
    // Not measured: ramps up to full rate so the API compiles its hot code and fills its connection pool.
    warmup: {
      executor: "ramping-arrival-rate",
      startRate: 10,
      timeUnit: "1s",
      stages: [{ target: 100, duration: "10s" }],
      preAllocatedVUs: 50,
      maxVUs: 100,
    },
    submissions: {
      executor: "constant-arrival-rate",
      startTime: "10s",
      rate: 100,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 50,
      maxVUs: 100,
    },
  },
  // Only the measured scenario counts.
  thresholds: {
    "http_req_failed{scenario:submissions}": ["rate<0.01"],
    "http_req_duration{scenario:submissions}": ["p(95)<200"],
    "checks{scenario:submissions}": ["rate>0.99"],
  },
};

const API_URL = __ENV.API_URL || "http://localhost:8080";
const STATES = ["CA", "TX", "FL", "WA", "IL", "NY"]; // NY is denied, which is also a 200.

// Waits until the API answers, so a container that is still starting doesn't count as errors.
export function setup() {
  for (let attempt = 0; attempt < 30; attempt++) {
    if (http.get(`${API_URL}/health`).status === 200) return;
    sleep(1);
  }
  fail(`${API_URL}/health didn't answer 200 within 30 s.`);
}

// SSNs starting with 666 are never issued, so these can't belong to anyone.
function randomSsn() {
  return "666" + String(Math.floor(Math.random() * 1000000)).padStart(6, "0");
}

export default function () {
  const response = http.post(
    `${API_URL}/api/v1/applications`,
    JSON.stringify({
      firstName: "Load",
      lastName: "Test",
      address: "1 Test St, Springfield, IL 62701",
      state: STATES[Math.floor(Math.random() * STATES.length)],
      companyName: "Load Test Inc",
      ssn: randomSsn(),
      requestedAmount: 1000 + Math.floor(Math.random() * 99000),
    }),
    { headers: { "Content-Type": "application/json" } },
  );
  check(response, { "decision returned": (r) => r.status === 200 });
}
