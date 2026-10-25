import type { Vehicle } from '@/api/types';

import { centerCommand, parseMapEvent, themeCommand, toStateCommand, zoomForRegion } from '../mapBridge';
import { isAllowedExternalUrl } from '../mapConfig';

const vehicle = (overrides: Partial<Vehicle> = {}): Vehicle => ({
  id: 'v1',
  latitude: 41.004,
  longitude: 29.002,
  batteryPercentage: 86,
  status: 'Available',
  brand: 'Segway Ninebot',
  rangeKm: 40,
  ...overrides,
});

describe('toStateCommand', () => {
  it('araçları batarya durumlarıyla, seçili aracı işaretleyerek çevirir', () => {
    const command = toStateCommand({
      vehicles: [
        vehicle(),
        vehicle({ id: 'v2', status: 'Reserved', batteryPercentage: 54.4 }),
        vehicle({ id: 'v3', batteryPercentage: 12 }),
      ],
      serviceAreas: [
        {
          name: 'Kadıköy',
          boundary: [
            { latitude: 1, longitude: 2 },
            { latitude: 3, longitude: 4 },
          ],
        },
      ],
      selectedId: 'v2',
      userLocation: { latitude: 41, longitude: 29 },
      bottomInset: 212.6,
    });

    expect(command).toEqual({
      type: 'state',
      vehicles: [
        expect.objectContaining({ id: 'v1', lat: 41.004, lng: 29.002, battery: 86, level: 'ok', selected: false }),
        expect.objectContaining({ id: 'v2', battery: 54, level: 'ok', selected: true }),
        expect.objectContaining({ id: 'v3', battery: 12, level: 'low', selected: false }),
      ],
      areas: [
        {
          name: 'Kadıköy',
          points: [
            [1, 2],
            [3, 4],
          ],
        },
      ],
      user: { lat: 41, lng: 29 },
      bottomInset: 213,
    });
    expect(command.type === 'state' && command.vehicles[0].label).toBe('Segway Ninebot, Müsait, batarya yüzde 86');
  });

  it('konum yoksa kullanıcı noktası gönderilmez', () => {
    const command = toStateCommand({
      vehicles: [],
      serviceAreas: [],
      selectedId: null,
      userLocation: null,
      bottomInset: -5,
    });
    expect(command).toMatchObject({ user: null, bottomInset: 0 });
  });

  it('odak ve tema komutları', () => {
    expect(centerCommand({ latitude: 37, longitude: 35.3 })).toEqual({ type: 'center', lat: 37, lng: 35.3 });
    expect(centerCommand({ latitude: 37, longitude: 35.3 }, 16)).toEqual({
      type: 'center',
      lat: 37,
      lng: 35.3,
      zoom: 16,
    });
    expect(themeCommand('dark')).toEqual({ type: 'theme', scheme: 'dark' });
  });
});

describe('parseMapEvent', () => {
  it('geçerli mesajları çözer', () => {
    expect(parseMapEvent('{"type":"ready"}')).toEqual({ type: 'ready' });
    expect(parseMapEvent({ type: 'select', id: 'v1' })).toEqual({ type: 'select', id: 'v1' });
    expect(parseMapEvent('{"type":"mapPress"}')).toEqual({ type: 'mapPress' });
    expect(
      parseMapEvent(
        JSON.stringify({
          type: 'region',
          region: { latitude: 41, longitude: 29, latitudeDelta: -0.02, longitudeDelta: 0.03 },
        }),
      ),
    ).toEqual({ type: 'region', region: { latitude: 41, longitude: 29, latitudeDelta: 0.02, longitudeDelta: 0.03 } });
  });

  it('bozuk ya da bilinmeyen mesajları yok sayar', () => {
    expect(parseMapEvent('{bozuk')).toBeNull();
    expect(parseMapEvent(null)).toBeNull();
    expect(parseMapEvent({ type: 'select' })).toBeNull();
    expect(parseMapEvent({ type: 'region', region: { latitude: 'x' } })).toBeNull();
    expect(
      parseMapEvent({ type: 'region', region: { latitude: 1, longitude: 2, latitudeDelta: NaN, longitudeDelta: 1 } }),
    ).toBeNull();
    expect(parseMapEvent({ type: 'navigate', url: 'https://example.com' })).toBeNull();
  });
});

describe('zoomForRegion', () => {
  it('panelin varsayılan görünümü (Leaflet 13) vektör haritada 12. seviyeye denk gelir', () => {
    expect(zoomForRegion({ latitude: 41, longitude: 29, latitudeDelta: 0.04, longitudeDelta: 0.04 })).toBe(12);
  });

  it('3 ile 19 arasında kalır', () => {
    expect(zoomForRegion({ latitude: 0, longitude: 0, latitudeDelta: 0, longitudeDelta: 0 })).toBe(19);
    expect(zoomForRegion({ latitude: 0, longitude: 0, latitudeDelta: 300, longitudeDelta: 300 })).toBe(3);
  });
});

describe('isAllowedExternalUrl', () => {
  it('yalnızca atıf bağlantılarına izin verir', () => {
    expect(isAllowedExternalUrl('https://www.openstreetmap.org/copyright')).toBe(true);
    expect(isAllowedExternalUrl('https://openfreemap.org')).toBe(true);
    expect(isAllowedExternalUrl('https://www.openmaptiles.org/')).toBe(true);
    expect(isAllowedExternalUrl('https://leafletjs.com')).toBe(false);
    expect(isAllowedExternalUrl('http://www.openstreetmap.org/copyright')).toBe(false);
    expect(isAllowedExternalUrl('https://www.openstreetmap.org.kotu.site/')).toBe(false);
    expect(isAllowedExternalUrl('javascript:alert(1)')).toBe(false);
  });
});
