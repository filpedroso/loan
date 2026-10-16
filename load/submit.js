// Load test for POST /api/v1/applications. Run it with `make load` while the stack is up.
import http from "k6/http";
import { check } from "k6";

export const options = {
  scenarios: {
    submissions: {
      executor: "constant-arrival-rate",
      rate: 100,
      timeUnit: "1s",
      duration: "30s",
      preAllocatedVUs: 50,
      maxVUs: 100,
    },
  },
  thresholds: {
    http_req_failed: ["rate<0.01"],
    http_req_duration: ["p(95)<200"],
    checks: ["rate>0.99"],
  },
};

const API_URL = __ENV.API_URL || "http://localhost:8080";
const STATES = ["CA", "TX", "FL", "WA", "IL", "NY"]; // NY is denied, which is also a 200.

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
