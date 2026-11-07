import type { Vehicle } from '@/api/types';

import { nearestRentable } from '../nearest';

const vehicle = (id: string, latitude: number, longitude: number, batteryPercentage = 80): Vehicle => ({
  id,
  latitude,
  longitude,
  batteryPercentage,
  status: 'Available',
  brand: 'Segway Ninebot',
  rangeKm: 40,
});

describe('nearestRentable', () => {
  const origin = { latitude: 41, longitude: 29 };

  it('en yakın aracı seçer', () => {
    const vehicles = [vehicle('uzak', 41.01, 29.01), vehicle('yakin', 41.001, 29.001), vehicle('orta', 41.004, 29)];
    expect(nearestRentable(vehicles, origin)?.id).toBe('yakin');
  });

  it('bataryası kiralamaya yetmeyeni atlar', () => {
    const vehicles = [vehicle('bos', 41.0001, 29.0001, 7), vehicle('dolu', 41.003, 29.003, 64)];
    expect(nearestRentable(vehicles, origin)?.id).toBe('dolu');
  });

  it('uygun araç yoksa null', () => {
    expect(nearestRentable([], origin)).toBeNull();
    expect(nearestRentable([vehicle('bos', 41, 29, 3)], origin)).toBeNull();
  });
});
