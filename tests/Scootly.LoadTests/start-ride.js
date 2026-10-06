import http from 'k6/http';
import exec from 'k6/execution';
import { sleep } from 'k6';
import { Counter, Trend } from 'k6/metrics';
import { BASE, authed, ensureUsers, loadVehicleIds } from './lib.js';

// Each virtual user owns one account and one vehicle: reserve -> start -> complete, repeated.
const VUS = Number(__ENV.VUS || 10);
const DURATION = __ENV.DURATION || '60s';
const RIDE_SECONDS = Number(__ENV.RIDE_SECONDS || 1);
const THINK_SECONDS = Number(__ENV.THINK_SECONDS || 1);

const completed = new Counter('rides_completed');
const reserveFailed = new Counter('rides_reserve_failed');
const startFailed = new Counter('rides_start_failed');
const completeFailed = new Counter('rides_complete_failed');
const flowMs = new Trend('ride_flow_ms', true);

export const options = {
  setupTimeout: '300s',
  scenarios: {
    rides: { executor: 'constant-vus', vus: VUS, duration: DURATION },
  },
  thresholds: {
    'http_req_duration{name:reserve}': ['p(95)>=0'],
    'http_req_duration{name:ride_start}': ['p(95)>=0'],
    'http_req_duration{name:ride_complete}': ['p(95)>=0'],
  },
};

export function setup() {
  return { users: ensureUsers(VUS), vehicleIds: loadVehicleIds(VUS, true) };
}

function report(step, res) {
  if (__ITER < 2) {
    console.error(`VU ${exec.vu.idInTest} ${step}: HTTP ${res.status} ${String(res.body).slice(0, 300)}`);
  }
}

export default function (data) {
  const idx = exec.vu.idInTest - 1;
  const user = data.users[idx];
  const vehicleId = data.vehicleIds[idx];
  const started = Date.now();

  const reserve = http.post(`${BASE}/api/v1/vehicles/${vehicleId}/reserve`, null, authed(user.token, 'reserve'));
  if (reserve.status !== 200) {
    reserveFailed.add(1);
    report('reserve', reserve);
    sleep(THINK_SECONDS);
    return;
  }

  const start = http.post(`${BASE}/api/rides/start`, JSON.stringify({ vehicleId: vehicleId }), authed(user.token, 'ride_start'));
  if (start.status !== 201) {
    startFailed.add(1);
    report('start', start);
    sleep(THINK_SECONDS);
    return;
  }

  const rideId = start.json('rideId') || start.json('RideId');
  sleep(RIDE_SECONDS);

  const complete = http.post(
    `${BASE}/api/rides/${rideId}/complete`,
    JSON.stringify({ endLatitude: 41.01, endLongitude: 29.01 }),
    authed(user.token, 'ride_complete'));

  if (complete.status !== 202) {
    completeFailed.add(1);
    report('complete', complete);
  } else {
    completed.add(1);
    flowMs.add(Date.now() - started);
  }

  sleep(THINK_SECONDS);
}