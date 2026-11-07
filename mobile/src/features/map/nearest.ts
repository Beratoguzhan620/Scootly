import type { Vehicle } from '@/api/types';
import { MIN_RENTABLE_BATTERY } from '@/domain/rules';
import { distanceMeters, type LatLng } from '@/lib/geo';

/** Kiralanabilecek (bataryası yeten) en yakın scooter; yoksa null. */
export function nearestRentable(vehicles: Vehicle[], origin: LatLng): Vehicle | null {
  let best: Vehicle | null = null;
  let bestDistance = Number.POSITIVE_INFINITY;
  for (const vehicle of vehicles) {
    if (vehicle.batteryPercentage < MIN_RENTABLE_BATTERY) continue;
    const distance = distanceMeters(origin, vehicle);
    if (distance < bestDistance) {
      best = vehicle;
      bestDistance = distance;
    }
  }
  return best;
}
