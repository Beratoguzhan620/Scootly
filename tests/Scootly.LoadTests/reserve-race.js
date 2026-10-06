import http from 'k6/http';
import exec from 'k6/execution';
import { sleep } from 'k6';
import { Counter } from 'k6/metrics';
import { BASE, authed, ensureUsers, loadVehicleIds } from './lib.js';

// Many different drivers reserve the SAME vehicle at the same moment: one success is expected, the rest 409.
const USERS = Number(__ENV.USERS || 20);

const won = new Counter('race_reserved_200');
const conflict = new Counter('race_conflict_409');
const other = new Counter('race_other_status');

export const options = {
  setupTimeout: '300s',
  scenarios: {
    race: { executor: 'per-vu-iterations', vus: USERS, iterations: 1, maxDuration: '120s' },
  },
};

export function setup() {
  return { users: ensureUsers(USERS), vehicleId: loadVehicleIds(1, true)[0] };
}

export default function (data) {
  const idx = exec.vu.idInTest - 1;
  const user = data.users[idx];
  const url = `${BASE}/api/v1/vehicles/${data.vehicleId}/reserve`;

  const res = http.post(url, null, authed(user.token, 'race_reserve'));

  if (res.status === 200) {
    won.add(1);
  } else if (res.status === 409) {
    conflict.add(1);
    if (idx < 3) {
      console.log(`VU ${idx + 1} got 409: ${String(res.body).slice(0, 300)}`);
    }
  } else {
    other.add(1);
    console.error(`VU ${idx + 1} unexpected HTTP ${res.status}: ${String(res.body).slice(0, 300)}`);
  }

  // Wait until every attempt has finished, then the winner releases the vehicle.
  sleep(5);

  if (res.status === 200) {
    http.del(`${BASE}/api/v1/vehicles/${data.vehicleId}/reservation`, null, authed(user.token, 'race_cancel'));
  }
}