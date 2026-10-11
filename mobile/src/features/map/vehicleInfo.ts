import type { Vehicle } from '@/api/types';

/** Modelin tam şarjlı menzili × batarya yüzdesi; kaba bir tahmin. */
export function estimatedRangeKm(vehicle: Pick<Vehicle, 'rangeKm' | 'batteryPercentage'>): number {
  return Math.max(0, Math.round((vehicle.rangeKm * vehicle.batteryPercentage) / 100));
}
