import * as Location from 'expo-location';

import type { LatLng } from './geo';

export type LocationPermission = 'checking' | 'granted' | 'denied';

const toLatLng = (position: Location.LocationObject): LatLng => ({
  latitude: position.coords.latitude,
  longitude: position.coords.longitude,
});

function withTimeout<T>(promise: Promise<T>, ms: number): Promise<T | null> {
  return new Promise((resolve, reject) => {
    const timer = setTimeout(() => resolve(null), ms);
    promise.then(
      (value) => {
        clearTimeout(timer);
        resolve(value);
      },
      (error: unknown) => {
        clearTimeout(timer);
        reject(error);
      },
    );
  });
}

/**
 * İzin verilmişse true. `request` true ise ve sistem izin veriyorsa kullanıcıya sorar; kullanıcı
 * kalıcı olarak reddettiyse tekrar sormaz (ayarlardan açması gerekir).
 */
export async function ensureLocationPermission(request = true): Promise<boolean> {
  try {
    const current = await Location.getForegroundPermissionsAsync();
    if (current.granted) return true;
    if (!request || !current.canAskAgain) return false;
    const requested = await Location.requestForegroundPermissionsAsync();
    return requested.granted;
  } catch {
    return false;
  }
}

export async function getLastKnownLocation(): Promise<LatLng | null> {
  try {
    const last = await Location.getLastKnownPositionAsync();
    return last ? toLatLng(last) : null;
  } catch {
    return null;
  }
}

/** Güncel konum; zaman aşımında ya da hata olursa son bilinen konum, o da yoksa null. */
export async function getCurrentLocation(timeoutMs = 8_000): Promise<LatLng | null> {
  try {
    const position = await withTimeout(
      Location.getCurrentPositionAsync({ accuracy: Location.Accuracy.Balanced }),
      timeoutMs,
    );
    if (position) return toLatLng(position);
  } catch {
    // aşağıda son bilinen konuma düş
  }
  return getLastKnownLocation();
}
