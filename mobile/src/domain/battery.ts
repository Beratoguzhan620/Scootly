import { LOW_BATTERY_THRESHOLD, MIN_RENTABLE_BATTERY } from './rules';

/**
 * Batarya durumu: `ok` kiralanabilir, `low` kiralanabilir ama saha ekibine görev açılacak kadar düşük,
 * `unrentable` rezerve edilemez.
 */
export type BatteryLevel = 'ok' | 'low' | 'unrentable';

export function batteryLevel(percentage: number): BatteryLevel {
  if (percentage < MIN_RENTABLE_BATTERY) return 'unrentable';
  if (percentage < LOW_BATTERY_THRESHOLD) return 'low';
  return 'ok';
}

/** 0–100 arası tam sayı */
export function batteryPercent(percentage: number): number {
  if (!Number.isFinite(percentage)) return 0;
  return Math.min(100, Math.max(0, Math.round(percentage)));
}
