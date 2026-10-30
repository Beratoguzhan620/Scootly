import { apiRequest, isApiStatus } from './client';
import type {
  Account,
  ActiveReservation,
  CompleteRideResponse,
  Guid,
  PagedResult,
  PaymentStatusResponse,
  RegisterVehicleRequest,
  RegisterVehicleResponse,
  Ride,
  ServiceArea,
  StartRideResponse,
  TokenResponse,
  Vehicle,
} from './types';
import { MAX_PAGE_SIZE } from '@/domain/rules';
import type { Bounds, LatLng } from '@/lib/geo';

/**
 * Uygulamanın kullandığı uçlar. Rotalar backend'deki gibi: `vehicles` ve `service-areas` sürümlü (`/api/v{n}`),
 * `auth`, `account`, `rides` ve `reservations` sürümsüz.
 */

export const authApi = {
  login: (email: string, password: string) =>
    apiRequest<TokenResponse>('/api/auth/login', { method: 'POST', body: { email, password }, auth: false }),

  /** 201 döner, token vermez; ardından giriş yapılır. Yeni hesap otomatik Driver rolü alır. */
  register: (email: string, password: string) =>
    apiRequest<void>('/api/auth/register', { method: 'POST', body: { email, password }, auth: false }),
};

export const accountApi = {
  get: (signal?: AbortSignal) => apiRequest<Account>('/api/account', { signal }),

  /** Eski token'lar geçersiz olur; yanıttaki taze token saklanmalı. */
  changePassword: (currentPassword: string, newPassword: string) =>
    apiRequest<TokenResponse>('/api/account/change-password', {
      method: 'POST',
      body: { currentPassword, newPassword },
    }),

  /** Açık rezervasyon, sürüş ya da borç varken 409 döner. */
  remove: (password: string) => apiRequest<void>('/api/account', { method: 'DELETE', body: { password } }),
};

export type VehicleQuery = {
  bounds?: Bounds;
  onlyAvailable?: boolean;
  pageNumber?: number;
  pageSize?: number;
};

export const vehiclesApi = {
  list: ({ bounds, onlyAvailable = true, pageNumber = 1, pageSize = 20 }: VehicleQuery, signal?: AbortSignal) =>
    apiRequest<PagedResult<Vehicle>>('/api/v2/vehicles', {
      query: { ...bounds, onlyAvailable, pageNumber, pageSize },
      signal,
    }),

  /** Müsait olmayan araç yalnızca onu rezerve etmiş ya da onunla sürüşte olan sürücüye görünür. */
  get: (id: Guid, signal?: AbortSignal) => apiRequest<Vehicle>(`/api/v2/vehicles/${id}`, { signal }),

  reserve: (id: Guid) => apiRequest<void>(`/api/v2/vehicles/${id}/reserve`, { method: 'POST' }),

  cancelReservation: (id: Guid) => apiRequest<void>(`/api/v2/vehicles/${id}/reservation`, { method: 'DELETE' }),
};

/**
 * Sunucudaki rezervasyon. `GET /api/reservations/active` backend'e sonradan eklendi; uç olmayan (eski) bir
 * sunucuda 404 döner ve uygulama cihazda sakladığı kayda döner.
 */
export type ServerReservation =
  | { kind: 'active'; reservation: ActiveReservation }
  | { kind: 'none' }
  | { kind: 'unsupported' };

export const reservationsApi = {
  /** Rezervasyon yoksa API 204 döner. */
  active: async (signal?: AbortSignal): Promise<ServerReservation> => {
    try {
      const reservation = await apiRequest<ActiveReservation | undefined>('/api/reservations/active', { signal });
      return reservation ? { kind: 'active', reservation } : { kind: 'none' };
    } catch (error) {
      if (isApiStatus(error, 404, 405)) return { kind: 'unsupported' };
      throw error;
    }
  },
};

/**
 * Filo ekibinin (FleetManager, FieldOperator) uçları. Filo ekibi müsait olmayan araçları da görür; araç kaydı
 * yalnızca filo yöneticisine açıktır.
 */
export const fleetApi = {
  list: ({ bounds, pageNumber = 1, pageSize = MAX_PAGE_SIZE }: Omit<VehicleQuery, 'onlyAvailable'>, signal?: AbortSignal) =>
    apiRequest<PagedResult<Vehicle>>('/api/v2/vehicles', {
      query: { ...bounds, onlyAvailable: false, pageNumber, pageSize },
      signal,
    }),

  /** Bütün filo, sayfa sayfa; en fazla `limit` araç. */
  listAll: async (signal?: AbortSignal, limit = 1_000): Promise<{ items: Vehicle[]; totalCount: number }> => {
    const items: Vehicle[] = [];
    let totalCount = 0;
    for (let pageNumber = 1; ; pageNumber += 1) {
      const page = await fleetApi.list({ pageNumber }, signal);
      items.push(...page.items);
      totalCount = page.totalCount;
      if (pageNumber >= page.totalPages || page.items.length === 0 || items.length >= limit) break;
    }
    return { items, totalCount };
  },

  get: (id: Guid, signal?: AbortSignal) => apiRequest<Vehicle>(`/api/v2/vehicles/${id}`, { signal }),

  /** Sürüşteki ve zaten bakımdaki araç için 409. Rezervasyon varsa düşer. */
  sendToMaintenance: (id: Guid) => apiRequest<void>(`/api/v2/vehicles/${id}/maintenance`, { method: 'POST' }),

  /** Sürüşteki ve zaten kayıp araç için 409. */
  markLost: (id: Guid) => apiRequest<void>(`/api/v2/vehicles/${id}/lost`, { method: 'POST' }),

  /** Yalnızca bakımdaki ya da kayıp araç hizmete döner. */
  returnToService: (id: Guid) => apiRequest<void>(`/api/v2/vehicles/${id}/return-to-service`, { method: 'POST' }),

  register: (request: RegisterVehicleRequest) =>
    apiRequest<RegisterVehicleResponse>('/api/v2/vehicles', { method: 'POST', body: request }),
};

export const serviceAreasApi = {
  list: (signal?: AbortSignal) => apiRequest<ServiceArea[]>('/api/v1/service-areas', { signal }),
};

export const ridesApi = {
  /** Aktif sürüş yoksa API 404 döner; burada `null` olur. */
  active: async (signal?: AbortSignal): Promise<Ride | null> => {
    try {
      return await apiRequest<Ride>('/api/rides/active', { signal });
    } catch (error) {
      if (isApiStatus(error, 404)) return null;
      throw error;
    }
  },

  get: (id: Guid, signal?: AbortSignal) => apiRequest<Ride>(`/api/rides/${id}`, { signal }),

  history: (pageNumber: number, pageSize = 20, signal?: AbortSignal) =>
    apiRequest<PagedResult<Ride>>('/api/rides', { query: { pageNumber, pageSize }, signal }),

  paymentStatus: (id: Guid, signal?: AbortSignal) =>
    apiRequest<PaymentStatusResponse>(`/api/rides/${id}/payment-status`, { signal }),

  /** Araç önce bu sürücü adına rezerve edilmiş olmalı. */
  start: (vehicleId: Guid) =>
    apiRequest<StartRideResponse>('/api/rides/start', { method: 'POST', body: { vehicleId } }),

  /** 202 döner; ödeme asenkron işlenir, durumu `paymentStatus` ile izlenir. */
  complete: (rideId: Guid, end: LatLng) =>
    apiRequest<CompleteRideResponse>(`/api/rides/${rideId}/complete`, {
      method: 'POST',
      body: { endLatitude: end.latitude, endLongitude: end.longitude },
    }),
};
