import { useCallback, useEffect, useState } from 'react';
import { AppState } from 'react-native';

import type { LatLng } from '@/lib/geo';
import {
  ensureLocationPermission,
  getCurrentLocation,
  getLastKnownLocation,
  type LocationPermission,
} from '@/lib/location';

/**
 * Harita için konum izni ve sürücünün konumu. İzin ilk açılışta bir kez sorulur; kullanıcı ayarlardan
 * izin verip uygulamaya dönerse yeniden sormadan kontrol edilir.
 */
export function useUserLocation() {
  const [permission, setPermission] = useState<LocationPermission>('checking');
  const [location, setLocation] = useState<LatLng | null>(null);

  const applyPermission = useCallback(async (granted: boolean): Promise<LatLng | null> => {
    setPermission(granted ? 'granted' : 'denied');
    if (!granted) return null;

    // Önce hızlı gelen son bilinen konum, ardından güncel konum.
    const last = await getLastKnownLocation();
    if (last) setLocation(last);
    const current = await getCurrentLocation();
    if (current) setLocation(current);
    return current ?? last;
  }, []);

  /** `request`: izin yoksa kullanıcıya sor (yalnızca kullanıcı bir şeye dokunduğunda ya da ilk açılışta). */
  const refresh = useCallback(
    async (request = true) => applyPermission(await ensureLocationPermission(request)),
    [applyPermission],
  );

  useEffect(() => {
    let active = true;
    void ensureLocationPermission(true).then((granted) => {
      if (active) void applyPermission(granted);
    });
    const subscription = AppState.addEventListener('change', (status) => {
      if (status === 'active') void refresh(false);
    });
    return () => {
      active = false;
      subscription.remove();
    };
  }, [applyPermission, refresh]);

  return { permission, location, refresh };
}
