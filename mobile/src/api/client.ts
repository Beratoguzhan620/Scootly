import type { ProblemDetails } from './types';

export type ApiErrorKind = 'http' | 'network' | 'timeout';

/** API'den dönen ya da API'ye ulaşılamayan her durum. `message` her zaman kullanıcıya gösterilebilir. */
export class ApiError extends Error {
  readonly kind: ApiErrorKind;
  readonly status: number | null;
  readonly title?: string;
  readonly detail?: string;
  readonly traceId?: string;
  readonly retryAfterSeconds?: number;

  constructor(init: {
    kind: ApiErrorKind;
    status: number | null;
    message: string;
    title?: string;
    detail?: string;
    traceId?: string;
    retryAfterSeconds?: number;
  }) {
    super(init.message);
    this.name = 'ApiError';
    this.kind = init.kind;
    this.status = init.status;
    this.title = init.title;
    this.detail = init.detail;
    this.traceId = init.traceId;
    this.retryAfterSeconds = init.retryAfterSeconds;
  }
}

type ApiConfig = {
  /** API adresi her istekte okunur; kullanıcı sunucu ayarlarından değiştirebilir. */
  getBaseUrl: () => string;
  getToken: () => string | null;
  /** Oturumlu bir istek 401 aldığında çağrılır (token süresi doldu ya da iptal edildi). */
  onUnauthorized: () => void;
  timeoutMs: number;
  /** Bağlantı hatası mesajında API adresini göster ve sunucu ayarlarına yönlendir (test sürümleri). */
  showBaseUrlInErrors: boolean;
  fetchImpl?: typeof fetch;
};

let config: ApiConfig = {
  getBaseUrl: () => '',
  getToken: () => null,
  onUnauthorized: () => {},
  timeoutMs: 15_000,
  showBaseUrlInErrors: false,
};

export function configureApi(partial: Partial<ApiConfig>): void {
  config = { ...config, ...partial };
}

export function getApiBaseUrl(): string {
  return config.getBaseUrl();
}

type QueryValue = string | number | boolean | null | undefined;

export type RequestOptions = {
  method?: 'GET' | 'POST' | 'PUT' | 'DELETE';
  body?: unknown;
  query?: Record<string, QueryValue>;
  /** false ise Authorization başlığı eklenmez (giriş, kayıt). */
  auth?: boolean;
  signal?: AbortSignal;
};

export function buildUrl(baseUrl: string, path: string, query?: Record<string, QueryValue>): string {
  const base = baseUrl.replace(/\/+$/, '');
  const params = Object.entries(query ?? {})
    .filter((entry): entry is [string, string | number | boolean] => entry[1] !== null && entry[1] !== undefined)
    .map(([key, value]) => `${encodeURIComponent(key)}=${encodeURIComponent(String(value))}`)
    .join('&');
  return `${base}${path}${params ? `?${params}` : ''}`;
}

export async function apiRequest<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const { method = 'GET', body, query, auth = true, signal } = options;
  const headers: Record<string, string> = { Accept: 'application/json' };
  if (body !== undefined) headers['Content-Type'] = 'application/json';

  const token = auth ? config.getToken() : null;
  if (token) headers.Authorization = `Bearer ${token}`;

  // Zaman aşımı ve çağıranın iptali (React Query) aynı AbortController'a bağlanır.
  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, config.timeoutMs);
  const forwardAbort = () => controller.abort();
  signal?.addEventListener('abort', forwardAbort);

  let response: Response;
  try {
    response = await (config.fetchImpl ?? fetch)(buildUrl(config.getBaseUrl(), path, query), {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal: controller.signal,
    });
  } catch (error) {
    if (signal?.aborted) throw error; // React Query iptali; sessizce geçer.
    if (timedOut) throw new ApiError({ kind: 'timeout', status: null, message: messages.timeout });
    throw new ApiError({ kind: 'network', status: null, message: messages.network(config) });
  } finally {
    clearTimeout(timer);
    signal?.removeEventListener('abort', forwardAbort);
  }

  if (response.ok) {
    if (response.status === 204) return undefined as T;
    const text = await response.text();
    return (text ? JSON.parse(text) : undefined) as T;
  }

  const problem = await readProblem(response);
  const error = toApiError(response.status, problem, response.headers.get('Retry-After'));
  if (response.status === 401 && token) config.onUnauthorized();
  throw error;
}

async function readProblem(response: Response): Promise<ProblemDetails | null> {
  const contentType = response.headers.get('Content-Type') ?? '';
  if (!contentType.includes('json')) return null;
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    return null;
  }
}

export function parseRetryAfter(value: string | null, now: number = Date.now()): number | undefined {
  if (!value) return undefined;
  const seconds = Number(value);
  if (Number.isFinite(seconds)) return Math.max(0, Math.ceil(seconds));
  const date = Date.parse(value);
  return Number.isNaN(date) ? undefined : Math.max(0, Math.ceil((date - now) / 1000));
}

/** HTTP yanıtını kullanıcıya gösterilecek Türkçe mesajla birlikte ApiError'a çevirir. */
export function toApiError(status: number, problem: ProblemDetails | null, retryAfterHeader: string | null): ApiError {
  const retryAfterSeconds = parseRetryAfter(retryAfterHeader);
  // API iş kuralı mesajlarını zaten Türkçe `detail` alanında yazıyor; onları olduğu gibi gösteriyoruz.
  // Model doğrulama hataları (`errors`) ise ASP.NET'in İngilizce varsayılanlarıdır.
  const detail = problem?.errors ? undefined : problem?.detail;

  let message: string;
  if (status === 429) {
    message = retryAfterSeconds
      ? `Çok fazla istek gönderildi. ${retryAfterSeconds} saniye sonra tekrar deneyin.`
      : 'Çok fazla istek gönderildi. Biraz bekleyip tekrar deneyin.';
  } else if (detail) {
    message = detail;
  } else if (status === 400) {
    message = 'Gönderilen bilgiler geçersiz.';
  } else if (status === 401) {
    message = 'Oturumunuz geçersiz. Lütfen tekrar giriş yapın.';
  } else if (status === 403) {
    message = 'Bu işlem için yetkiniz yok.';
  } else if (status === 404) {
    message = 'Kayıt bulunamadı.';
  } else if (status === 503) {
    message = 'Servis şu anda yoğun. Lütfen biraz sonra tekrar deneyin.';
  } else if (status >= 500) {
    message = 'Sunucuda beklenmeyen bir hata oluştu. Lütfen tekrar deneyin.';
  } else {
    message = problem?.title ?? 'İstek tamamlanamadı.';
  }

  if (status >= 500 && problem?.traceId) message += ` (İzleme kimliği: ${problem.traceId})`;

  return new ApiError({
    kind: 'http',
    status,
    message,
    title: problem?.title,
    detail: problem?.detail,
    traceId: problem?.traceId,
    retryAfterSeconds,
  });
}

const messages = {
  timeout: 'Sunucu zamanında yanıt vermedi. Bağlantınızı kontrol edip tekrar deneyin.',
  network: (current: ApiConfig) =>
    current.showBaseUrlInErrors
      ? `Sunucuya ulaşılamadı (${current.getBaseUrl()}). API'nin çalıştığını ve telefonun bu adrese erişebildiğini kontrol edin; adres yanlışsa giriş ekranındaki sunucu ayarlarından değiştirin.`
      : 'Sunucuya ulaşılamadı. İnternet bağlantınızı kontrol edip tekrar deneyin.',
};

/** Herhangi bir hatayı ekranda gösterilecek metne çevirir. */
export function errorMessage(error: unknown, fallback = 'Beklenmeyen bir hata oluştu.'): string {
  if (error instanceof ApiError) return error.message;
  if (error instanceof Error && error.message) return error.message;
  return fallback;
}

export function isApiStatus(error: unknown, ...statuses: number[]): error is ApiError {
  return error instanceof ApiError && error.status !== null && statuses.includes(error.status);
}
