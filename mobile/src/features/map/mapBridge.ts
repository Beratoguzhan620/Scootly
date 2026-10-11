import type { ServiceArea, Vehicle } from '@/api/types';
import { vehicleStatusMeta } from '@/domain/labels';
import type { LatLng, MapRegion } from '@/lib/geo';

/**
 * Uygulama ile harita sayfası (WebView/iframe içindeki Leaflet) arasındaki mesajlar. İki taraf da yalnızca
 * JSON metni alışverişi yapar; sayfadaki karşılığı `mapHtml.ts` içindeki betiktir.
 */

export type MapVehicle = {
  id: string;
  lat: number;
  lng: number;
  battery: number;
  /** Rozet tonu (`VehicleStatusTagHelper` eşlemesi): success, warning, primary, secondary, danger, light */
  tone: string;
  /** Ekran okuyucu metni */
  label: string;
  selected: boolean;
};

export type MapArea = {
  name: string;
  points: [number, number][];
};

/** Uygulamadan sayfaya */
export type MapCommand =
  | {
      type: 'state';
      vehicles: MapVehicle[];
      areas: MapArea[];
      user: { lat: number; lng: number } | null;
      /** Haritanın altını kaplayan kartın yüksekliği (px). */
      bottomInset: number;
    }
  | { type: 'center'; lat: number; lng: number; zoom?: number };

/** Sayfadan uygulamaya */
export type MapEvent =
  | { type: 'ready' }
  | { type: 'region'; region: MapRegion }
  | { type: 'select'; id: string }
  | { type: 'mapPress' }
  | { type: 'openUrl'; url: string }
  | { type: 'error'; message: string };

export type MapStateInput = {
  vehicles: Vehicle[];
  serviceAreas: ServiceArea[];
  selectedId: string | null;
  userLocation: LatLng | null;
  bottomInset: number;
};

export function toStateCommand({ vehicles, serviceAreas, selectedId, userLocation, bottomInset }: MapStateInput): MapCommand {
  return {
    type: 'state',
    vehicles: vehicles.map((vehicle) => {
      const meta = vehicleStatusMeta(vehicle.status);
      return {
        id: vehicle.id,
        lat: vehicle.latitude,
        lng: vehicle.longitude,
        battery: Math.round(vehicle.batteryPercentage),
        tone: meta.tone,
        label: `${vehicle.brand}, ${meta.label}, batarya yüzde ${vehicle.batteryPercentage}`,
        selected: vehicle.id === selectedId,
      };
    }),
    areas: serviceAreas.map((area) => ({
      name: area.name,
      points: area.boundary.map((point): [number, number] => [point.latitude, point.longitude]),
    })),
    user: userLocation ? { lat: userLocation.latitude, lng: userLocation.longitude } : null,
    bottomInset: Math.max(0, Math.round(bottomInset)),
  };
}

export function centerCommand(point: LatLng, zoom?: number): MapCommand {
  return { type: 'center', lat: point.latitude, lng: point.longitude, zoom };
}

const isFiniteNumber = (value: unknown): value is number => typeof value === 'number' && Number.isFinite(value);

/** Sayfadan gelen veriyi doğrular; beklenmeyen her şey yok sayılır. */
export function parseMapEvent(data: unknown): MapEvent | null {
  let message: unknown = data;
  if (typeof data === 'string') {
    try {
      message = JSON.parse(data);
    } catch {
      return null;
    }
  }
  if (typeof message !== 'object' || message === null) return null;
  const event = message as Record<string, unknown>;

  switch (event.type) {
    case 'ready':
    case 'mapPress':
      return { type: event.type };
    case 'select':
      return typeof event.id === 'string' && event.id ? { type: 'select', id: event.id } : null;
    case 'openUrl':
      return typeof event.url === 'string' ? { type: 'openUrl', url: event.url } : null;
    case 'error':
      return { type: 'error', message: String(event.message ?? '') };
    case 'region': {
      const region = event.region as Record<string, unknown> | undefined;
      if (
        !region ||
        !isFiniteNumber(region.latitude) ||
        !isFiniteNumber(region.longitude) ||
        !isFiniteNumber(region.latitudeDelta) ||
        !isFiniteNumber(region.longitudeDelta)
      ) {
        return null;
      }
      return {
        type: 'region',
        region: {
          latitude: region.latitude,
          longitude: region.longitude,
          latitudeDelta: Math.abs(region.latitudeDelta),
          longitudeDelta: Math.abs(region.longitudeDelta),
        },
      };
    }
    default:
      return null;
  }
}

/** Bir bölgeyi gösterecek Leaflet yakınlık seviyesi. */
export function zoomForRegion(region: MapRegion): number {
  const zoom = Math.round(Math.log2(360 / Math.max(region.longitudeDelta, 1e-6)));
  return Math.min(19, Math.max(3, zoom));
}
