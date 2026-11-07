export type LatLng = { latitude: number; longitude: number };

export type Bounds = {
  minLatitude: number;
  maxLatitude: number;
  minLongitude: number;
  maxLongitude: number;
};

export type MapRegion = LatLng & { latitudeDelta: number; longitudeDelta: number };

/** Paneldeki haritanın varsayılan görünümü (`map.js` → `setView([41.0, 29.0], 13)`); 0,04° ≈ yakınlık 13. */
export const DEFAULT_REGION: MapRegion = {
  latitude: 41.0,
  longitude: 29.0,
  latitudeDelta: 0.04,
  longitudeDelta: 0.04,
};

const clamp = (value: number, min: number, max: number) => Math.min(max, Math.max(min, value));

/**
 * Görünen harita alanını API'nin dikdörtgen filtresine çevirir. Sınırlar dışa doğru yuvarlanır: hem görünen
 * alanın tamamı kapsanır hem de küçük kaydırmalarda sorgu anahtarı değişmez (gereksiz istek atılmaz).
 */
export function regionToBounds(region: MapRegion, decimals = 3): Bounds {
  const factor = 10 ** decimals;
  const down = (value: number) => Math.floor(value * factor) / factor;
  const up = (value: number) => Math.ceil(value * factor) / factor;
  const halfLat = region.latitudeDelta / 2;
  const halfLon = region.longitudeDelta / 2;

  return {
    minLatitude: clamp(down(region.latitude - halfLat), -90, 90),
    maxLatitude: clamp(up(region.latitude + halfLat), -90, 90),
    minLongitude: clamp(down(region.longitude - halfLon), -180, 180),
    maxLongitude: clamp(up(region.longitude + halfLon), -180, 180),
  };
}

export function isInBounds(point: LatLng, bounds: Bounds): boolean {
  return (
    point.latitude >= bounds.minLatitude &&
    point.latitude <= bounds.maxLatitude &&
    point.longitude >= bounds.minLongitude &&
    point.longitude <= bounds.maxLongitude
  );
}

const EARTH_RADIUS_M = 6_371_000;
const toRad = (deg: number) => (deg * Math.PI) / 180;

/** İki nokta arası kuş uçuşu mesafe (metre, haversine). */
export function distanceMeters(a: LatLng, b: LatLng): number {
  const dLat = toRad(b.latitude - a.latitude);
  const dLon = toRad(b.longitude - a.longitude);
  const h =
    Math.sin(dLat / 2) ** 2 + Math.cos(toRad(a.latitude)) * Math.cos(toRad(b.latitude)) * Math.sin(dLon / 2) ** 2;
  return 2 * EARTH_RADIUS_M * Math.asin(Math.min(1, Math.sqrt(h)));
}

/** Bir noktanın etrafında verilen yarıçapı gösterecek harita bölgesi. */
export function regionAround(point: LatLng, delta = 0.012): MapRegion {
  return { ...point, latitudeDelta: delta, longitudeDelta: delta };
}
