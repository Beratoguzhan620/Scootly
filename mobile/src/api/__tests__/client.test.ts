import { ApiError, apiRequest, buildUrl, configureApi, parseRetryAfter, toApiError } from '../client';

type FakeResponseInit = { status: number; body?: unknown; headers?: Record<string, string> };

/** Testlerin ihtiyaç duyduğu kadar Response. */
function fakeResponse({ status, body, headers = {} }: FakeResponseInit): Response {
  const text = body === undefined ? '' : JSON.stringify(body);
  const lower = Object.fromEntries(Object.entries(headers).map(([key, value]) => [key.toLowerCase(), value]));
  return {
    ok: status >= 200 && status < 300,
    status,
    headers: { get: (name: string) => lower[name.toLowerCase()] ?? null },
    text: async () => text,
    json: async () => JSON.parse(text),
  } as unknown as Response;
}

describe('buildUrl', () => {
  it('sondaki eğik çizgiyi atar, sorguyu kodlar, boş değerleri atlar', () => {
    expect(
      buildUrl('http://10.0.2.2:5016/', '/api/v2/vehicles', {
        minLatitude: 40.98,
        onlyAvailable: true,
        brand: 'Ş&G',
        pageNumber: undefined,
        maxLatitude: null,
      }),
    ).toBe('http://10.0.2.2:5016/api/v2/vehicles?minLatitude=40.98&onlyAvailable=true&brand=%C5%9E%26G');
  });
});

describe('toApiError', () => {
  it('API’nin Türkçe iş kuralı mesajını gösterir', () => {
    const error = toApiError(
      409,
      { title: 'İş kuralı ihlali', detail: 'Araç rezerve edilmemiş, sürüş başlatılamaz.' },
      null,
    );
    expect(error.message).toBe('Araç rezerve edilmemiş, sürüş başlatılamaz.');
    expect(error.status).toBe(409);
  });

  it('hız sınırında bekleme süresini söyler', () => {
    expect(toApiError(429, null, '30').message).toBe('Çok fazla istek gönderildi. 30 saniye sonra tekrar deneyin.');
  });

  it('sunucu hatasına izleme kimliğini ekler', () => {
    const error = toApiError(500, { detail: 'Beklenmeyen bir hata oluştu.', traceId: '00-abc-01' }, null);
    expect(error.message).toContain('İzleme kimliği: 00-abc-01');
  });

  it('ASP.NET model doğrulama hatalarının İngilizce metnini göstermez', () => {
    const error = toApiError(400, { title: 'One or more validation errors occurred.', errors: { vehicleId: ['x'] } }, null);
    expect(error.message).toBe('Gönderilen bilgiler geçersiz.');
  });

  it('gövdesiz 401 için Türkçe mesaj üretir', () => {
    expect(toApiError(401, null, null).message).toBe('Oturumunuz geçersiz. Lütfen tekrar giriş yapın.');
  });
});

describe('parseRetryAfter', () => {
  it('saniye ve HTTP tarihi biçimlerini okur', () => {
    expect(parseRetryAfter('12')).toBe(12);
    const now = Date.UTC(2026, 9, 11, 8, 0, 0);
    expect(parseRetryAfter('Sun, 11 Oct 2026 08:00:45 GMT', now)).toBe(45);
    expect(parseRetryAfter('anlamsız')).toBeUndefined();
  });
});

describe('apiRequest', () => {
  const fetchMock = jest.fn<Promise<Response>, [string, RequestInit]>();
  const onUnauthorized = jest.fn();
  let token: string | null = 'token-123';

  beforeEach(() => {
    fetchMock.mockReset();
    onUnauthorized.mockReset();
    token = 'token-123';
    configureApi({
      getBaseUrl: () => 'http://api.test',
      getToken: () => token,
      onUnauthorized,
      fetchImpl: fetchMock as unknown as typeof fetch,
      timeoutMs: 1_000,
      showBaseUrlInErrors: true,
    });
  });

  it('token ve JSON gövdesini gönderir, yanıtı çözer', async () => {
    fetchMock.mockResolvedValue(fakeResponse({ status: 201, body: { rideId: 'r1' } }));

    await expect(apiRequest('/api/rides/start', { method: 'POST', body: { vehicleId: 'v1' } })).resolves.toEqual({
      rideId: 'r1',
    });

    const [url, init] = fetchMock.mock.calls[0];
    expect(url).toBe('http://api.test/api/rides/start');
    expect(init.method).toBe('POST');
    expect(init.body).toBe(JSON.stringify({ vehicleId: 'v1' }));
    expect(init.headers).toMatchObject({ Authorization: 'Bearer token-123', 'Content-Type': 'application/json' });
  });

  it('auth: false ise token göndermez', async () => {
    fetchMock.mockResolvedValue(fakeResponse({ status: 200, body: { token: 'yeni' } }));
    await apiRequest('/api/auth/login', { method: 'POST', body: {}, auth: false });
    expect(fetchMock.mock.calls[0][1].headers).not.toHaveProperty('Authorization');
  });

  it('204 için undefined döner', async () => {
    fetchMock.mockResolvedValue(fakeResponse({ status: 204 }));
    await expect(apiRequest('/api/v2/vehicles/v1/reservation', { method: 'DELETE' })).resolves.toBeUndefined();
  });

  it('oturumlu istekte 401 gelince oturumu kapattırır', async () => {
    fetchMock.mockResolvedValue(fakeResponse({ status: 401 }));
    await expect(apiRequest('/api/rides/active')).rejects.toMatchObject({ status: 401 });
    expect(onUnauthorized).toHaveBeenCalledTimes(1);
  });

  it('oturumsuz istekte 401 oturumu kapattırmaz (hatalı parola)', async () => {
    token = null;
    fetchMock.mockResolvedValue(fakeResponse({ status: 401 }));
    await expect(apiRequest('/api/auth/login', { method: 'POST', auth: false })).rejects.toBeInstanceOf(ApiError);
    expect(onUnauthorized).not.toHaveBeenCalled();
  });

  it('ProblemDetails mesajını ApiError olarak fırlatır', async () => {
    fetchMock.mockResolvedValue(
      fakeResponse({
        status: 409,
        body: { title: 'İşlem gerçekleştirilemedi', detail: 'Zaten aktif bir rezervasyonunuz var.' },
        headers: { 'Content-Type': 'application/problem+json; charset=utf-8' },
      }),
    );
    await expect(apiRequest('/api/v2/vehicles/v1/reserve', { method: 'POST' })).rejects.toMatchObject({
      kind: 'http',
      status: 409,
      message: 'Zaten aktif bir rezervasyonunuz var.',
    });
  });

  it('ağ hatasını API adresiyle birlikte anlatır', async () => {
    fetchMock.mockRejectedValue(new TypeError('Network request failed'));
    await expect(apiRequest('/api/account')).rejects.toMatchObject({
      kind: 'network',
      status: null,
      message: expect.stringContaining('http://api.test'),
    });
  });

  it('çağıran iptal ederse özgün hatayı iletir (React Query iptali)', async () => {
    const controller = new AbortController();
    const abortError = new Error('Aborted');
    fetchMock.mockImplementation(async () => {
      controller.abort();
      throw abortError;
    });
    await expect(apiRequest('/api/account', { signal: controller.signal })).rejects.toBe(abortError);
  });
});
