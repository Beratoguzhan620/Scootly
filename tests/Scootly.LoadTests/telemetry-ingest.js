import http from 'k6/http';
import { Counter } from 'k6/metrics';
import { BASE, JSON_HEADERS, authed, loadVehicleIds } from './lib.js';

// Device batches (up to 500 readings). RATES is a comma list of batches per second, STAGE_SECONDS per step.
const RATES = (__ENV.RATES || '1,2,4').split(',').map(Number);
const STAGE_SECONDS = Number(__ENV.STAGE_SECONDS || 30);
const BATCH = Math.min(Number(__ENV.BATCH || 500), 500);

const ok = new Counter('telemetry_2xx');
const tooMany = new Counter('telemetry_429');
const backpressure = new Counter('telemetry_503');
const otherStatus = new Counter('telemetry_other');

export const options = {
  setupTimeout: '120s',
  scenarios: {
    ingest: {
      executor: 'ramping-arrival-rate',
      startRate: RATES[0],
      timeUnit: '1s',
      preAllocatedVUs: 20,
      maxVUs: 200,
      stages: RATES.map((rate) => ({ target: rate, duration: `${STAGE_SECONDS}s` })),
    },
  },
};

export function setup() {
  const secret = __ENV.DEVICE_CLIENT_SECRET;
  if (!secret) {
    throw new Error('DEVICE_CLIENT_SECRET is not set');
  }

  const clientId = __ENV.DEVICE_CLIENT_ID || 'scootly-device-simulator';
  const auth = http.post(`${BASE}/api/device-auth/token`, JSON.stringify({ clientId: clientId, clientSecret: secret }),
    { headers: JSON_HEADERS, tags: { name: 'setup_device_auth' } });

  if (auth.status !== 200) {
    throw new Error(`device auth failed: HTTP ${auth.status}`);
  }

  return { token: auth.json('token') || auth.json('Token'), vehicleIds: loadVehicleIds(BATCH, false) };
}

export default function (data) {
  const now = new Date().toISOString();

  // Battery 50-89 stays above any low-battery threshold, so no field tasks are generated.
  const readings = data.vehicleIds.map((id, i) => ({
    vehicleId: id,
    latitude: 41.0 + (i % 50) * 0.001,
    longitude: 29.0 + (i % 50) * 0.001,
    batteryPercentage: 50 + (i % 40),
    recordedAt: now,
  }));

  const res = http.post(`${BASE}/api/telemetry/batch`, JSON.stringify({ readings: readings }), authed(data.token, 'telemetry_batch'));

  if (res.status >= 200 && res.status < 300) {
    ok.add(1);
  } else if (res.status === 429) {
    tooMany.add(1);
  } else if (res.status === 503) {
    backpressure.add(1);
  } else {
    otherStatus.add(1);
  }
}