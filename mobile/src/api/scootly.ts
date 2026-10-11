import { apiRequest, isApiStatus } from './client';
import type {
  Account,
  CompleteRideResponse,
  Guid,
  PagedResult,
  PaymentStatusResponse,
  Ride,
  ServiceArea,
  StartRideResponse,
  TokenResponse,
  Vehicle,
} from './types';
import type { Bounds, LatLng } from '@/lib/geo';

/**
 * Sürücü uygulamasının kullandığı uçlar. Rotalar backend'deki gibi: `vehicles` ve `service-areas`
 * sürümlü (`/api/v{n}`), `auth`, `account` ve `rides` sürümsüz.
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
