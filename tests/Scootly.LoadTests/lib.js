import http from 'k6/http';

export const BASE = __ENV.BASE_URL || 'http://host.docker.internal:5016';
export const JSON_HEADERS = { 'Content-Type': 'application/json' };
export const USER_PASSWORD = 'LoadTest1234';

// Request params with a bearer token and a stable "name" tag (URLs contain ids; without the tag k6 makes one series per id).
export function authed(token, name) {
  return {
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    tags: { name: name },
  };
}

// Registers (first run: 201, later runs: 400 "already registered", ignored) and logs in.
export function ensureUser(index) {
  const email = `lt-user-${index}@scootly.test`;
  const body = JSON.stringify({ email: email, password: USER_PASSWORD });

  http.post(`${BASE}/api/auth/register`, body, { headers: JSON_HEADERS, tags: { name: 'setup_register' } });
  const login = http.post(`${BASE}/api/auth/login`, body, { headers: JSON_HEADERS, tags: { name: 'setup_login' } });

  if (login.status !== 200) {
    throw new Error(`login failed for ${email}: HTTP ${login.status} ${login.body}`);
  }

  return { email: email, token: login.json('token') || login.json('Token') };
}

export function ensureUsers(count) {
  const users = [];
  for (let i = 1; i <= count; i++) {
    users.push(ensureUser(i));
  }
  return users;
}

// Reads vehicle ids from the public v2 list (100 per page).
export function loadVehicleIds(max, onlyAvailable) {
  const ids = [];

  for (let page = 1; ids.length < max && page <= 200; page++) {
    const res = http.get(`${BASE}/api/v2/vehicles?pageNumber=${page}&pageSize=100`, { tags: { name: 'setup_vehicles' } });

    if (res.status !== 200) {
      throw new Error(`vehicle list failed: HTTP ${res.status} ${res.body}`);
    }

    const body = res.json();
    const items = body.items || body.Items || [];

    for (const v of items) {
      const status = String(v.status || v.Status || '').toLowerCase();
      if (!onlyAvailable || status === 'available') {
        ids.push(v.id || v.Id);
      }
    }

    if (page >= (body.totalPages || body.TotalPages || 1)) {
      break;
    }
  }

  if (ids.length === 0) {
    throw new Error('no vehicles found (is the database empty or are all vehicles busy?)');
  }

  return ids.slice(0, max);
}