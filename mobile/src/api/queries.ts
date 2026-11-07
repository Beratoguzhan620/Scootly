import { keepPreviousData, useInfiniteQuery, useQuery } from '@tanstack/react-query';

import { fleetApi, ridesApi, serviceAreasApi, vehiclesApi } from './scootly';
import { MAX_PAGE_SIZE } from '@/domain/rules';
import type { Bounds } from '@/lib/geo';

export const queryKeys = {
  vehicles: ['vehicles'] as const,
  vehiclesInBounds: (bounds: Bounds | null) => ['vehicles', 'list', bounds] as const,
  vehicle: (id: string) => ['vehicles', 'detail', id] as const,
  serviceAreas: ['service-areas'] as const,
  rides: ['rides'] as const,
  activeRide: ['rides', 'active'] as const,
  ride: (id: string) => ['rides', 'detail', id] as const,
  payment: (id: string) => ['rides', 'payment', id] as const,
  rideHistory: ['rides', 'history'] as const,
  /** Filo ekibinin sorguları ayrı tutulur: aynı araç sürücüye farklı (yalnızca müsaitse) görünür. */
  fleet: ['fleet'] as const,
  fleetInBounds: (bounds: Bounds | null) => ['fleet', 'list', bounds] as const,
  fleetAll: ['fleet', 'all'] as const,
  fleetVehicle: (id: string) => ['fleet', 'detail', id] as const,
};

/** Paneldeki harita da araçları 30 saniyede bir yeniliyor. */
const MAP_REFRESH_MS = 30_000;

/** Görünen alandaki müsait araçlar. Sayfa boyutu API'nin izin verdiği en büyük değer. */
export function useVehiclesInBounds(bounds: Bounds | null, enabled = true) {
  return useQuery({
    queryKey: queryKeys.vehiclesInBounds(bounds),
    queryFn: ({ signal }) =>
      vehiclesApi.list({ bounds: bounds ?? undefined, onlyAvailable: true, pageSize: MAX_PAGE_SIZE }, signal),
    enabled: enabled && bounds !== null,
    placeholderData: keepPreviousData,
    refetchInterval: enabled ? MAP_REFRESH_MS : false,
  });
}

export function useVehicle(id: string | null | undefined) {
  return useQuery({
    queryKey: queryKeys.vehicle(id ?? ''),
    queryFn: ({ signal }) => vehiclesApi.get(id as string, signal),
    enabled: Boolean(id),
  });
}

export function useServiceAreas() {
  return useQuery({
    queryKey: queryKeys.serviceAreas,
    queryFn: ({ signal }) => serviceAreasApi.list(signal),
    staleTime: 10 * 60_000,
  });
}

/** Sürüş, Worker'ın terk edilmiş sürüş işiyle sunucuda da bitebilir; arada bir kontrol edilir. */
export function useActiveRide(enabled = true) {
  return useQuery({
    queryKey: queryKeys.activeRide,
    queryFn: ({ signal }) => ridesApi.active(signal),
    enabled,
    refetchInterval: 60_000,
  });
}

export function useRide(id: string) {
  return useQuery({
    queryKey: queryKeys.ride(id),
    queryFn: ({ signal }) => ridesApi.get(id, signal),
  });
}

/**
 * Ödeme, outbox → kuyruk → ödeme sağlayıcısı üzerinden asenkron sonuçlanır. "Bekleniyor" durumunda önce
 * sık, sonra seyrek sorulur (reddedilen ödeme sunucuda ancak birkaç dakika sonra yeniden denenir).
 */
export function usePaymentStatus(id: string, enabled = true) {
  return useQuery({
    queryKey: queryKeys.payment(id),
    queryFn: ({ signal }) => ridesApi.paymentStatus(id, signal),
    enabled,
    refetchInterval: (query) => {
      if (query.state.data?.paymentStatus !== 'Pending') return false;
      return query.state.dataUpdateCount < 20 ? 3_000 : 15_000;
    },
  });
}

export function useRideHistory() {
  return useInfiniteQuery({
    queryKey: queryKeys.rideHistory,
    queryFn: ({ pageParam, signal }) => ridesApi.history(pageParam, 20, signal),
    initialPageParam: 1,
    getNextPageParam: (lastPage) => (lastPage.pageNumber < lastPage.totalPages ? lastPage.pageNumber + 1 : undefined),
  });
}

/** Filo haritası: görünen alandaki bütün araçlar, durumlarıyla. */
export function useFleetInBounds(bounds: Bounds | null) {
  return useQuery({
    queryKey: queryKeys.fleetInBounds(bounds),
    queryFn: ({ signal }) => fleetApi.list({ bounds: bounds ?? undefined, pageSize: MAX_PAGE_SIZE }, signal),
    enabled: bounds !== null,
    placeholderData: keepPreviousData,
    refetchInterval: 20_000,
  });
}

/** Araç listesi: bütün filo (en fazla 1.000 araç). */
export function useFleetAll() {
  return useQuery({
    queryKey: queryKeys.fleetAll,
    queryFn: ({ signal }) => fleetApi.listAll(signal),
    refetchInterval: 60_000,
  });
}

export function useFleetVehicle(id: string) {
  return useQuery({
    queryKey: queryKeys.fleetVehicle(id),
    queryFn: ({ signal }) => fleetApi.get(id, signal),
    refetchInterval: 20_000,
  });
}
