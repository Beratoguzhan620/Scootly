import type { Vehicle, VehicleStatus } from '@/api/types';

/**
 * Filo ekibinin araç işlemleri ve durum geçişleri (`Scootly.Domain.Fleet.Vehicle`):
 * Available/Reserved/Lost → Maintenance → Available; Available/Reserved/Maintenance → Lost → Available.
 * Sürüşteki araca dokunulamaz; bakıma alınan ya da kayıp işaretlenen aracın rezervasyonu düşer.
 */
export type FleetAction = 'maintenance' | 'lost' | 'service';

export const VEHICLE_STATUSES: VehicleStatus[] = ['Available', 'Reserved', 'InRide', 'Maintenance', 'Lost'];

export function availableActions(status: VehicleStatus): FleetAction[] {
  switch (status) {
    case 'Available':
    case 'Reserved':
      return ['maintenance', 'lost'];
    case 'Maintenance':
      return ['service', 'lost'];
    case 'Lost':
      return ['service', 'maintenance'];
    default:
      return [];
  }
}

type ActionMeta = {
  /** Düğme ve onay penceresi */
  label: string;
  /** İşlem bitince */
  done: string;
  /** Onay penceresindeki açıklama */
  confirm: (status: VehicleStatus) => string;
  destructive: boolean;
};

export const fleetActionMeta: Record<FleetAction, ActionMeta> = {
  maintenance: {
    label: 'Bakıma al',
    done: 'Araç bakıma alındı.',
    confirm: (status) =>
      status === 'Reserved'
        ? 'Araç kiralamadan çekilir; sürücünün rezervasyonu iptal olur.'
        : 'Araç, hizmete döndürülene kadar kiralamadan çekilir.',
    destructive: false,
  },
  lost: {
    label: 'Kayıp olarak işaretle',
    done: 'Araç kayıp olarak işaretlendi.',
    confirm: (status) =>
      status === 'Reserved'
        ? 'Araç kiralamadan çekilir; sürücünün rezervasyonu iptal olur. Bulununca hizmete döndürebilirsin.'
        : 'Araç kiralamadan çekilir. Bulununca hizmete döndürebilirsin.',
    destructive: true,
  },
  service: {
    label: 'Hizmete döndür',
    done: 'Araç yeniden müsait.',
    confirm: () => 'Araç müsait olur ve sürücüler kiralayabilir.',
    destructive: false,
  },
};

/** Sürüşteki aracın neden düzenlenemediği. */
export const IN_RIDE_NOTE = 'Sürüşteki araç bakıma alınamaz ya da kayıp işaretlenemez; önce sürüşün bitmesi gerekir.';

export type StatusCounts = Record<VehicleStatus, number> & { total: number };

export function countByStatus(vehicles: Pick<Vehicle, 'status'>[]): StatusCounts {
  const counts: StatusCounts = { Available: 0, Reserved: 0, InRide: 0, Maintenance: 0, Lost: 0, total: 0 };
  for (const vehicle of vehicles) {
    if (vehicle.status in counts) counts[vehicle.status] += 1;
    counts.total += 1;
  }
  return counts;
}

/** Liste araması: plaka kodu (kimliğin başı) ya da marka. */
export function matchesSearch(vehicle: Pick<Vehicle, 'id' | 'brand'>, query: string): boolean {
  const text = query.trim().toLocaleLowerCase('tr-TR');
  if (!text) return true;
  return (
    vehicle.id.toLowerCase().startsWith(text.replace(/\s/g, '')) || vehicle.brand.toLocaleLowerCase('tr-TR').includes(text)
  );
}

/** Önce dikkat isteyenler (kayıp, bakım), sonra sürüşte/rezerve, en son müsait; aynı durumda düşük batarya önce. */
const ORDER: Record<VehicleStatus, number> = { Lost: 0, Maintenance: 1, InRide: 2, Reserved: 3, Available: 4 };

export function sortForFleet<T extends Pick<Vehicle, 'status' | 'batteryPercentage' | 'id'>>(vehicles: T[]): T[] {
  return [...vehicles].sort(
    (a, b) =>
      (ORDER[a.status] ?? 9) - (ORDER[b.status] ?? 9) ||
      a.batteryPercentage - b.batteryPercentage ||
      a.id.localeCompare(b.id),
  );
}

/** Yeni araç formu (`RegisterVehicleRequestValidator` ile aynı sınırlar). */
export const BRAND_MAX_LENGTH = 100;
export const MAX_RANGE_KM = 1_000;

export type NewVehicleInput = { brand: string; rangeKm: string; batteryPercentage: string; latitude: string; longitude: string };

export type NewVehicleValidation =
  | {
      ok: true;
      request: { brand: string; rangeKm: number; batteryPercentage: number; latitude: number; longitude: number };
    }
  | { ok: false; errors: Partial<Record<keyof NewVehicleInput, string>> };

const toNumber = (value: string) => Number(value.trim().replace(',', '.'));

export function validateNewVehicle(input: NewVehicleInput): NewVehicleValidation {
  const errors: Partial<Record<keyof NewVehicleInput, string>> = {};
  const brand = input.brand.trim();
  if (!brand) errors.brand = 'Markayı yaz.';
  else if (brand.length > BRAND_MAX_LENGTH) errors.brand = `En fazla ${BRAND_MAX_LENGTH} karakter.`;

  const rangeKm = toNumber(input.rangeKm);
  if (!Number.isInteger(rangeKm) || rangeKm < 1 || rangeKm > MAX_RANGE_KM) {
    errors.rangeKm = `1-${MAX_RANGE_KM} arasında tam sayı.`;
  }

  const battery = toNumber(input.batteryPercentage);
  if (!Number.isInteger(battery) || battery < 0 || battery > 100) errors.batteryPercentage = '0-100 arasında tam sayı.';

  const latitude = toNumber(input.latitude);
  const longitude = toNumber(input.longitude);
  if (!input.latitude.trim() || !Number.isFinite(latitude) || latitude < -90 || latitude > 90) {
    errors.latitude = 'Enlem -90 ile 90 arasında olmalı.';
  }
  if (!input.longitude.trim() || !Number.isFinite(longitude) || longitude < -180 || longitude > 180) {
    errors.longitude = 'Boylam -180 ile 180 arasında olmalı.';
  }

  if (Object.keys(errors).length > 0) return { ok: false, errors };
  return { ok: true, request: { brand, rangeKm, batteryPercentage: battery, latitude, longitude } };
}
