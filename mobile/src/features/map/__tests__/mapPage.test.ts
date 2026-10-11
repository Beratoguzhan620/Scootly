import { JSDOM } from 'jsdom';

import { toStateCommand } from '../mapBridge';
import { buildMapHtml, toScriptJson } from '../mapHtml';

/**
 * Harita sayfasını (Leaflet + köprü betiği) jsdom içinde gerçekten çalıştırır; WebView'daki davranışın
 * mantık kısmını cihazsız doğrular.
 */
function loadPage(attribution = '&copy; OpenStreetMap') {
  const messages: Record<string, unknown>[] = [];
  const html = buildMapHtml({
    center: [41, 29],
    zoom: 13,
    focusZoom: 16,
    tileUrl: 'https://tile.example/{z}/{x}/{y}.png',
    attribution,
  });
  const dom = new JSDOM(html, {
    runScripts: 'dangerously',
    pretendToBeVisual: true,
    beforeParse(window) {
      (window as unknown as { ReactNativeWebView: unknown }).ReactNativeWebView = {
        postMessage: (data: string) => messages.push(JSON.parse(data)),
      };
    },
  });
  const page = dom.window as unknown as Window & { __scootly: { receive: (json: string) => void } };
  return { dom, page, messages };
}

const state = (selectedId: string | null = null) =>
  JSON.stringify(
    toStateCommand({
      vehicles: [
        {
          id: 'v1',
          latitude: 41.004,
          longitude: 29.002,
          batteryPercentage: 86,
          status: 'Available',
          brand: 'Segway',
          rangeKm: 40,
        },
        {
          id: 'v2',
          latitude: 40.998,
          longitude: 29.006,
          batteryPercentage: 54,
          status: 'Reserved',
          brand: 'Xiaomi',
          rangeKm: 30,
        },
      ],
      serviceAreas: [
        {
          name: 'Bölge',
          boundary: [
            { latitude: 40.99, longitude: 28.99 },
            { latitude: 41.01, longitude: 28.99 },
            { latitude: 41.01, longitude: 29.01 },
          ],
        },
      ],
      selectedId,
      userLocation: { latitude: 41, longitude: 29 },
      bottomInset: 200,
    }),
  );

describe('harita sayfası', () => {
  it('açılınca hazır olduğunu ve görünen alanı bildirir', () => {
    const { messages, dom } = loadPage();
    expect(messages[0]).toEqual({ type: 'ready' });
    expect(messages[1]).toMatchObject({ type: 'region', region: { latitude: expect.any(Number) } });
    dom.window.close();
  });

  it('durumu çizer: araç işaretleri, kullanıcı noktası ve hizmet bölgesi', () => {
    const { page, dom } = loadPage();
    page.__scootly.receive(state('v2'));

    const pins = Array.from(page.document.querySelectorAll('.pin'));
    expect(pins.map((pin) => pin.textContent)).toEqual(['%86', '%54']);
    expect(pins[0].className).toBe('pin pin-success');
    expect(pins[1].className).toBe('pin pin-warning pin-selected');
    expect(pins[0].getAttribute('aria-label')).toBe('Segway, Müsait, batarya yüzde 86');
    expect(page.document.querySelectorAll('.user-dot')).toHaveLength(1);
    expect(page.document.querySelectorAll('path.leaflet-interactive, path')).not.toHaveLength(0);
    dom.window.close();
  });

  it('güncellemede işaretleri yeniden kullanır, listeden çıkanı kaldırır', () => {
    const { page, dom } = loadPage();
    page.__scootly.receive(state());
    const command = JSON.parse(state());
    command.vehicles = [{ ...command.vehicles[0], battery: 80 }];
    page.__scootly.receive(JSON.stringify(command));

    const pins = Array.from(page.document.querySelectorAll('.pin'));
    expect(pins.map((pin) => pin.textContent)).toEqual(['%80']);
    dom.window.close();
  });

  it('işarete dokununca aracı seçer, boş alana dokununca seçimi bırakır', () => {
    const { page, messages, dom } = loadPage();
    page.__scootly.receive(state());
    messages.length = 0;

    const pin = page.document.querySelector('.pin') as HTMLElement;
    pin.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true }));
    expect(messages).toContainEqual({ type: 'select', id: 'v1' });
    expect(messages).not.toContainEqual({ type: 'mapPress' });

    messages.length = 0;
    const container = page.document.getElementById('map') as HTMLElement;
    container.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true, clientX: 5, clientY: 5 }));
    expect(messages).toContainEqual({ type: 'mapPress' });
    dom.window.close();
  });

  it('atıf bağlantısını sayfada açmaz, uygulamaya iletir', () => {
    const { page, messages, dom } = loadPage(
      '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>',
    );
    messages.length = 0;
    const link = page.document.querySelector('.leaflet-control-attribution a') as HTMLAnchorElement;
    link.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true, cancelable: true }));
    expect(messages).toEqual([{ type: 'openUrl', url: 'https://www.openstreetmap.org/copyright' }]);
    dom.window.close();
  });

  it('bozuk komutları yok sayar', () => {
    const { page, dom } = loadPage();
    expect(() => page.__scootly.receive('{bozuk')).not.toThrow();
    expect(() => page.__scootly.receive('null')).not.toThrow();
    dom.window.close();
  });
});

describe('toScriptJson', () => {
  it('</script> ve satır ayırıcılarını kaçışlar', () => {
    const json = toScriptJson({ text: `</script><script>alert(1)</script>${String.fromCharCode(0x2028)}` });
    expect(json).not.toContain('</script');
    expect(json).not.toContain(String.fromCharCode(0x2028));
    expect(JSON.parse(json)).toEqual({ text: `</script><script>alert(1)</script>${String.fromCharCode(0x2028)}` });
  });

  it('atıf metnindeki </script> sayfayı bozmaz', () => {
    const { page, messages, dom } = loadPage('</script><script>window.kotu = true</script>');
    expect((page as unknown as { kotu?: boolean }).kotu).toBeUndefined();
    expect(messages[0]).toEqual({ type: 'ready' });
    dom.window.close();
  });
});
