import { availableActions, countByStatus, matchesSearch, sortForFleet, validateNewVehicle } from '../fleet';

describe('filo işlemleri', () => {
  it("backend'in durum geçişlerine uyar; sürüşteki araca dokunulmaz", () => {
    expect(availableActions('Available')).toEqual(['maintenance', 'lost']);
    expect(availableActions('Reserved')).toEqual(['maintenance', 'lost']);
    expect(availableActions('Maintenance')).toEqual(['service', 'lost']);
    expect(availableActions('Lost')).toEqual(['service', 'maintenance']);
    expect(availableActions('InRide')).toEqual([]);
  });

  it('durumları sayar', () => {
    expect(
      countByStatus([{ status: 'Available' }, { status: 'Available' }, { status: 'Lost' }, { status: 'InRide' }]),
    ).toEqual({ Available: 2, Reserved: 0, InRide: 1, Maintenance: 0, Lost: 1, total: 4 });
  });

  it('plaka kodu ya da markayla arar', () => {
    const vehicle = { id: '3f2b8c1a-1111-4000-8000-000000000001', brand: 'Xiaomi Pro 2' };
    expect(matchesSearch(vehicle, '3F2B')).toBe(true);
    expect(matchesSearch(vehicle, 'xiaomi')).toBe(true);
    expect(matchesSearch(vehicle, 'segway')).toBe(false);
    expect(matchesSearch(vehicle, '  ')).toBe(true);
  });

  it('dikkat isteyenleri öne alır', () => {
    const sorted = sortForFleet([
      { id: 'a', status: 'Available', batteryPercentage: 10 },
      { id: 'b', status: 'Lost', batteryPercentage: 90 },
      { id: 'c', status: 'Maintenance', batteryPercentage: 50 },
      { id: 'd', status: 'Available', batteryPercentage: 5 },
    ]);
    expect(sorted.map((vehicle) => vehicle.id)).toEqual(['b', 'c', 'd', 'a']);
  });
});

describe('yeni araç formu', () => {
  const valid = { brand: ' Segway Ninebot ', rangeKm: '40', batteryPercentage: '100', latitude: '41,0012', longitude: '29.0021' };

  it('geçerli formu isteğe çevirir (virgüllü ondalık da olur)', () => {
    expect(validateNewVehicle(valid)).toEqual({
      ok: true,
      request: { brand: 'Segway Ninebot', rangeKm: 40, batteryPercentage: 100, latitude: 41.0012, longitude: 29.0021 },
    });
  });

  it("backend'in sınırlarını uygular", () => {
    const result = validateNewVehicle({ brand: '', rangeKm: '0', batteryPercentage: '101', latitude: '95', longitude: '' });
    expect(result).toEqual({
      ok: false,
      errors: {
        brand: 'Markayı yaz.',
        rangeKm: '1-1000 arasında tam sayı.',
        batteryPercentage: '0-100 arasında tam sayı.',
        latitude: 'Enlem -90 ile 90 arasında olmalı.',
        longitude: 'Boylam -180 ile 180 arasında olmalı.',
      },
    });
  });
});
