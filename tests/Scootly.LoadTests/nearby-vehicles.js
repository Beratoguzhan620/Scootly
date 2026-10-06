import http from 'k6/http';
import { check } from 'k6';
import { BASE } from './lib.js';

// Anonymous reads of the nearby-vehicles list: half default query (served from the cache), half bounding-box query (database).
const RATE = Number(__ENV.RATE || 20);
const DURATION = __ENV.DURATION || '60s';

export const options = {
  scenarios: {
    nearby: {
      executor: 'constant-arrival-rate',
      rate: RATE,
      timeUnit: '1s',
      duration: DURATION,
      preAllocatedVUs: 50,
      maxVUs: 300,
    },
  },
  // Always-true thresholds only make k6 print the per-query sub-metrics; no target numbers are asserted.
  thresholds: {
    'http_req_duration{query:default}': ['p(95)>=0'],
    'http_req_duration{query:bbox}': ['p(95)>=0'],
  },
};

export default function () {
  let url;
  let kind;

  if (Math.random() < 0.5) {
    kind = 'default';
    url = `${BASE}/api/v1/vehicles`;
  } else {
    kind = 'bbox';
    const lat = 40.95 + Math.random() * 0.1;
    const lon = 28.9 + Math.random() * 0.2;
    url = `${BASE}/api/v1/vehicles?minLatitude=${lat.toFixed(4)}&maxLatitude=${(lat + 0.05).toFixed(4)}` +
      `&minLongitude=${lon.toFixed(4)}&maxLongitude=${(lon + 0.05).toFixed(4)}&onlyAvailable=true&pageSize=50`;
  }

  const res = http.get(url, { tags: { query: kind, name: `nearby_${kind}` } });
  check(res, { 'status is 200': (r) => r.status === 200 });
}