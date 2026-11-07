import type { QueryClient } from '@tanstack/react-query';

import { isApiStatus } from '@/api/client';
import { queryKeys } from '@/api/queries';
import { vehiclesApi } from '@/api/scootly';
import type { Vehicle } from '@/api/types';
import { MAX_PAGE_SIZE } from '@/domain/rules';
import { matchVehicleCode, type ScannedVehicle } from '@/domain/vehicleCode';
import { regionAround, regionToBounds, type LatLng } from '@/lib/geo';

/** Plaka koduyla aranırken sürücünün çevresinde bakılan alan (~1,5 km). */
const CODE_SEARCH_DELTA = 0.03;

type Context = {
  queryClient: QueryClient;
  reservedVehicleId: string | null;
  location: LatLng | null;
  signal?: AbortSignal;
};

/** Önbellekteki (haritada yüklenmiş) araçlar. */
function cachedVehicles(queryClient: QueryClient): Vehicle[] {
  return queryClient.getQueriesData<unknown>({ queryKey: queryKeys.vehicles }).flatMap(([, data]) => {
    if (!data || typeof data !== 'object') return [];
    if ('items' in data && Array.isArray(data.items)) return data.items as Vehicle[];
    if ('id' in data && typeof data.id === 'string') return [data as Vehicle];
    return [];
  });
}

/**
 * Okutulan QR'daki ya da elle girilen koddaki scooter'ı bulur ve güncel durumunu getirir. Plaka kodu kimliğin
 * yalnızca ilk 8 karakteri olduğu için önce sürücünün rezervasyonu, sonra haritada yüklü araçlar, en son sürücünün
 * çevresindeki müsait araçlar aranır.
 */
export async function resolveVehicle(target: ScannedVehicle, context: Context): Promise<Vehicle> {
  let id = target.kind === 'id' ? target.id : null;

  if (target.kind === 'code') {
    const { reservedVehicleId, queryClient, location, signal } = context;
    if (reservedVehicleId?.toLowerCase().startsWith(target.code)) id = reservedVehicleId;
    id ??= matchVehicleCode(cachedVehicles(queryClient), target.code)?.id ?? null;
    if (!id && location) {
      const nearby = await vehiclesApi.list(
        { bounds: regionToBounds(regionAround(location, CODE_SEARCH_DELTA)), onlyAvailable: true, pageSize: MAX_PAGE_SIZE },
        signal,
      );
      id = matchVehicleCode(nearby.items, target.code)?.id ?? null;
    }
    if (!id) {
      throw new Error('Bu kodla yakında müsait bir scooter bulunamadı. Kodu kontrol et ya da QR kodu okut.');
    }
  }

  try {
    return await vehiclesApi.get(id as string, context.signal);
  } catch (error) {
    // Müsait olmayan araç, onu kullanmayan sürücüye "yok" görünür.
    if (isApiStatus(error, 404)) {
      throw new Error('Bu scooter şu anda kiralanamıyor: başka bir sürücüde ya da bakımda olabilir.');
    }
    throw error;
  }
}
