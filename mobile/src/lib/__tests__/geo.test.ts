import { distanceMeters, isInBounds, regionToBounds } from '../geo';

describe('regionToBounds', () => {
  const region = { latitude: 41.0, longitude: 29.0, latitudeDelta: 0.04, longitudeDelta: 0.06 };

  it('görünen alanı tamamen kapsar', () => {
    const bounds = regionToBounds(region);
    expect(bounds.minLatitude).toBeLessThanOrEqual(40.98);
    expect(bounds.maxLatitude).toBeGreaterThanOrEqual(41.02);
    expect(bounds.minLongitude).toBeLessThanOrEqual(28.97);
    expect(bounds.maxLongitude).toBeGreaterThanOrEqual(29.03);
  });

  it('küçük kaydırmalarda aynı sınırları üretir (gereksiz istek atılmaz)', () => {
    const moved = { ...region, latitude: region.latitude + 0.0001 };
    expect(regionToBounds(moved)).toEqual(regionToBounds({ ...region, latitude: region.latitude + 0.0002 }));
  });

  it('enlem ve boylamı geçerli aralıkta tutar', () => {
    const bounds = regionToBounds({ latitude: 89.9, longitude: 179.9, latitudeDelta: 1, longitudeDelta: 1 });
    expect(bounds.maxLatitude).toBe(90);
    expect(bounds.maxLongitude).toBe(180);
  });
});

describe('distanceMeters', () => {
  it('1 derece enlem ≈ 111 km', () => {
    const meters = distanceMeters({ latitude: 40, longitude: 29 }, { latitude: 41, longitude: 29 });
    expect(meters).toBeGreaterThan(111_000);
    expect(meters).toBeLessThan(111_400);
  });

  it('aynı nokta için 0', () => {
    expect(distanceMeters({ latitude: 37, longitude: 35.3 }, { latitude: 37, longitude: 35.3 })).toBe(0);
  });
});

it('isInBounds sınırları kapsar', () => {
  const bounds = { minLatitude: 40, maxLatitude: 41, minLongitude: 28, maxLongitude: 29 };
  expect(isInBounds({ latitude: 40.5, longitude: 28.5 }, bounds)).toBe(true);
  expect(isInBounds({ latitude: 41, longitude: 29 }, bounds)).toBe(true);
  expect(isInBounds({ latitude: 41.01, longitude: 28.5 }, bounds)).toBe(false);
});
