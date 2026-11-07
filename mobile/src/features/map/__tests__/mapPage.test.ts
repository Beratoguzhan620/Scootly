import { JSDOM, VirtualConsole } from 'jsdom';

import type { Vehicle } from '@/api/types';

import { toStateCommand } from '../mapBridge';
import { buildMapHtml, toScriptJson } from '../mapHtml';
import { mapPalettes } from '../mapTheme';

/**
 * Harita sayfasının köprü betiğini jsdom içinde çalıştırır. jsdom'da WebGL olmadığı için MapLibre yerine
 * çağrıları kaydeden sahte bir kütüphane enjekte edilir; sayfanın mantığı (durum → kaynaklar, dokunuş →
 * mesaj, tema → stil) cihazsız doğrulanır.
 */

type Feature = { geometry: { coordinates: unknown }; properties: Record<string, unknown> };
type Source = { data: { features: Feature[] }; cluster?: boolean };
type Layer = { id: string; type: string; layout?: Record<string, unknown>; paint?: Record<string, unknown> };
type Style = { layers: Layer[] };

class FakeMap {
  static instances: FakeMap[] = [];
  static fail = false;
  handlers: Record<string, ((event: unknown) => void)[]> = {};
  sources: Record<string, Source> = {};
  layers: Layer[] = [];
  styles: Style[] = [];
  easeCalls: Record<string, unknown>[] = [];
  queryResult: unknown[] = [];
  control: unknown = null;
  rotationDisabled = false;
  touchZoomRotate = { disableRotation: () => (this.rotationDisabled = true) };

  constructor(
    public options: { style: Style; zoom: number },
    private window: Window,
  ) {
    if (FakeMap.fail) throw new Error('WebGL yok');
    FakeMap.instances.push(this);
    this.setStyle(options.style);
  }
  on(type: string, handler: (event: unknown) => void) {
    (this.handlers[type] ??= []).push(handler);
    return this;
  }
  fire(type: string, event: unknown = {}) {
    for (const handler of this.handlers[type] ?? []) handler(event);
  }
  addControl(control: unknown) {
    this.control = control;
  }
  setStyle(style: Style) {
    this.styles.push(style);
    this.sources = {};
    this.layers = [];
    // Gerçek kütüphanede stil eşzamansız yüklenir.
    this.window.setTimeout(() => this.fire('style.load'), 0);
  }
  getStyle() {
    return { layers: [...(this.styles[this.styles.length - 1].layers ?? []), ...this.layers] };
  }
  addSource(id: string, source: Source & { data: Source['data'] }) {
    this.sources[id] = {
      ...source,
      setData: (data: Source['data']) => (this.sources[id].data = data),
      getClusterExpansionZoom: () => Promise.resolve(15),
    } as Source;
  }
  getSource(id: string) {
    return this.sources[id];
  }
  addLayer(layer: Layer) {
    this.layers.push(layer);
  }
  getLayer(id: string) {
    return this.layers.find((layer) => layer.id === id);
  }
  setLayoutProperty() {}
  hasImage() {
    return false;
  }
  addImage() {}
  queryRenderedFeatures() {
    return this.queryResult;
  }
  // 1 derece = 10.000 px; (29, 41) ekranın ortası.
  project([lng, lat]: [number, number]) {
    return { x: (lng - 29) * 10_000 + 195, y: (41 - lat) * 10_000 + 422 };
  }
  unproject([x, y]: [number, number]) {
    return { lng: 29 + (x - 195) / 10_000, lat: 41 - (y - 422) / 10_000 };
  }
  getContainer() {
    return { clientWidth: 390, clientHeight: 844 };
  }
  getZoom() {
    return this.options.zoom;
  }
  easeTo(options: Record<string, unknown>) {
    this.easeCalls.push(options);
  }
}

const tick = (ms = 5) => new Promise((resolve) => setTimeout(resolve, ms));

function loadPage({ styleUrl = '', fetchStyle }: { styleUrl?: string; fetchStyle?: () => Promise<unknown> } = {}) {
  const messages: Record<string, unknown>[] = [];
  const html = buildMapHtml(
    { center: [41, 29], zoom: 13, focusZoom: 15, styleUrl, scheme: 'light', palettes: mapPalettes },
    { embedLibrary: false },
  );
  const virtualConsole = new VirtualConsole(); // jsdom'un "canvas yok" uyarıları test çıktısını kirletmesin
  const dom = new JSDOM(html, {
    runScripts: 'dangerously',
    pretendToBeVisual: true,
    virtualConsole,
    beforeParse(window) {
      const target = window as unknown as Record<string, unknown>;
      target.ReactNativeWebView = { postMessage: (data: string) => messages.push(JSON.parse(data)) };
      target.maplibregl = {
        Map: class extends FakeMap {
          constructor(options: { style: Style; zoom: number }) {
            super(options, window as unknown as Window);
          }
        },
        AttributionControl: class {},
      };
      if (fetchStyle) {
        target.fetch = () =>
          fetchStyle().then((style) => ({ ok: true, status: 200, json: () => Promise.resolve(style) }));
      }
    },
  });
  const page = dom.window as unknown as Window & { __scootly: { receive: (json: string) => void } };
  const map = () => FakeMap.instances[FakeMap.instances.length - 1];
  return { dom, page, messages, map };
}

const vehicle = (id: string, latitude: number, longitude: number, batteryPercentage: number): Vehicle => ({
  id,
  latitude,
  longitude,
  batteryPercentage,
  status: 'Available',
  brand: 'Segway',
  rangeKm: 40,
});

const state = (selectedId: string | null = null, bottomInset = 200) =>
  JSON.stringify(
    toStateCommand({
      vehicles: [
        vehicle('v1', 41.004, 29.002, 86),
        vehicle('v2', 40.998, 29.006, 54),
        vehicle('v3', 41.001, 28.996, 7),
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
      bottomInset,
    }),
  );

beforeEach(() => {
  FakeMap.instances = [];
  FakeMap.fail = false;
});

describe('harita sayfası', () => {
  it('açılınca hazır olduğunu ve görünen alanı bildirir; döndürme kapalı', () => {
    const { messages, map, dom } = loadPage();
    expect(messages[0]).toEqual({ type: 'ready' });
    expect(messages[1]).toMatchObject({ type: 'region', region: { latitude: expect.any(Number) } });
    expect(map().rotationDisabled).toBe(true);
    dom.window.close();
  });

  it('durumu kaynaklara yazar: seçili araç kümeye girmez, alan [boylam, enlem] sırasıyla kapanır', async () => {
    const { page, map, dom } = loadPage();
    await tick();
    page.__scootly.receive(state('v2'));

    const sources = map().sources;
    expect(sources.vehicles.cluster).toBe(true);
    expect(sources.vehicles.data.features.map((feature) => feature.properties)).toEqual([
      expect.objectContaining({ id: 'v1', icon: 'p|86|ok|a|light' }),
      expect.objectContaining({ id: 'v3', icon: 'p|7|unrentable|a|light' }),
    ]);
    expect(sources.selected.data.features.map((feature) => feature.properties)).toEqual([
      { id: 'v2', icon: 's|54|ok|a|light' },
    ]);
    expect(sources.user.data.features).toHaveLength(1);
    const ring = sources.areas.data.features[0].geometry.coordinates as number[][][];
    expect(ring[0][0]).toEqual([28.99, 40.99]);
    expect(ring[0][ring[0].length - 1]).toEqual(ring[0][0]);
    dom.window.close();
  });

  it('filo haritasında işaret adı aracın durumunu taşır', async () => {
    const { page, map, dom } = loadPage();
    await tick();
    page.__scootly.receive(
      JSON.stringify(
        toStateCommand({
          vehicles: [
            { ...vehicle('v1', 41.004, 29.002, 86), status: 'Reserved' },
            { ...vehicle('v2', 40.998, 29.006, 54), status: 'Lost' },
          ],
          serviceAreas: [],
          selectedId: 'v2',
          userLocation: null,
          bottomInset: 200,
          statusPins: true,
        }),
      ),
    );
    expect(map().sources.vehicles.data.features.map((feature) => feature.properties.icon)).toEqual(['p|86|ok|r|light']);
    expect(map().sources.selected.data.features[0].properties.icon).toBe('s|54|ok|l|light');
    dom.window.close();
  });

  it('panelin altında kalan alanı bildirmez; seçili araç gizliyse görünür alana kaydırır', async () => {
    const { page, map, messages, dom } = loadPage();
    await tick();
    messages.length = 0;
    // v2 ekranın ortasının altında (y ≈ 442); 500 px'lik panel onu örter.
    page.__scootly.receive(state('v2', 500));

    const region = messages.find((message) => message.type === 'region') as { region: { latitude: number } };
    expect(region.region.latitude).toBeGreaterThan(41);
    expect(map().easeCalls[0]).toMatchObject({ center: [29.006, 40.998] });
    dom.window.close();
  });

  it('işarete dokununca aracı seçer, kümeye dokununca yakınlaşır, boş alana dokununca bırakır', async () => {
    const { page, map, messages, dom } = loadPage();
    await tick();
    page.__scootly.receive(state());
    messages.length = 0;

    map().queryResult = [
      { layer: { id: 'pins' }, properties: { id: 'v1' }, geometry: { coordinates: [29.002, 41.004] } },
    ];
    map().fire('click', { point: { x: 215, y: 382 } });
    expect(messages).toEqual([{ type: 'select', id: 'v1' }]);

    map().queryResult = [
      { layer: { id: 'clusters' }, properties: { cluster_id: 4 }, geometry: { coordinates: [29.001, 41.001] } },
    ];
    map().fire('click', { point: { x: 205, y: 412 } });
    await tick();
    expect(map().easeCalls.pop()).toMatchObject({ center: [29.001, 41.001], zoom: 15.5 });

    messages.length = 0;
    map().queryResult = [];
    map().fire('click', { point: { x: 5, y: 5 } });
    expect(messages).toEqual([{ type: 'mapPress' }]);
    dom.window.close();
  });

  it('tema değişince stili yeniden kurar ve işaretleri koyu temayla çizer', async () => {
    const { page, map, dom } = loadPage();
    await tick();
    page.__scootly.receive(state());
    page.__scootly.receive(JSON.stringify({ type: 'theme', scheme: 'dark' }));
    await tick();

    const styles = map().styles;
    expect(styles[styles.length - 1].layers[0].paint).toEqual({ 'background-color': mapPalettes.dark.background });
    expect(map().sources.vehicles.data.features[0].properties.icon).toBe('p|86|ok|a|dark');
    expect(JSON.stringify(map().getLayer('clusters')?.layout)).toContain('dark');
    expect(page.document.documentElement.className).toBe('dark');
    dom.window.close();
  });

  it('altlık stilini uygulamanın renklerine boyar, otoyol tabelalarını gizler', async () => {
    const base = {
      version: 8,
      sources: {},
      layers: [
        { id: 'background', type: 'background', paint: { 'background-color': '#fff' } },
        { id: 'water', type: 'fill', 'source-layer': 'water', paint: { 'fill-color': '#00f' } },
        { id: 'park', type: 'fill', 'source-layer': 'park', paint: { 'fill-color': '#0f0' } },
        { id: 'highway_minor', type: 'line', 'source-layer': 'transportation', paint: { 'line-color': '#ccc' } },
        { id: 'highway_motorway_inner', type: 'line', 'source-layer': 'transportation', paint: {} },
        { id: 'highway-shield-non-us', type: 'symbol', 'source-layer': 'transportation_name', layout: {} },
        { id: 'label_city', type: 'symbol', 'source-layer': 'place', paint: { 'text-color': '#000' } },
      ],
    };
    const { map, dom } = loadPage({ styleUrl: 'https://tiles.example/style', fetchStyle: () => Promise.resolve(base) });
    await tick(20);

    const style = map().styles[map().styles.length - 1];
    const byId = Object.fromEntries(style.layers.map((layer) => [layer.id, layer]));
    const colors = mapPalettes.light.base;
    expect(byId.background.paint).toEqual({ 'background-color': colors.land });
    expect(byId.water.paint?.['fill-color']).toBe(colors.water);
    expect(byId.park.paint?.['fill-color']).toBe(colors.park);
    expect(byId.highway_minor.paint?.['line-color']).toBe(colors.roadMinor);
    expect(byId.highway_motorway_inner.paint?.['line-color']).toBe(colors.motorway);
    expect(byId['highway-shield-non-us'].layout?.visibility).toBe('none');
    expect(byId.label_city.paint).toEqual({ 'text-color': colors.placeLabel, 'text-halo-color': colors.halo });
    // Uygulamanın katmanları etiketlerin altına, araçlar en üste eklenir.
    expect(map().getLayer('pins')).toBeDefined();
    dom.window.close();
  });

  it('stil alınamazsa düz zeminle devam eder ve bunu bildirir', async () => {
    const { map, messages, dom } = loadPage({
      styleUrl: 'https://tiles.example/style',
      fetchStyle: () => Promise.reject(new Error('çevrimdışı')),
    });
    await tick(20);
    expect(map().styles).toHaveLength(1);
    expect(messages).toContainEqual({ type: 'error', message: 'Harita stili alınamadı: çevrimdışı' });
    dom.window.close();
  });

  it('atıf bağlantısını sayfada açmaz, uygulamaya iletir', () => {
    const { page, messages, dom } = loadPage();
    messages.length = 0;
    const link = page.document.createElement('a');
    link.href = 'https://www.openstreetmap.org/copyright';
    page.document.body.appendChild(link);
    link.dispatchEvent(new dom.window.MouseEvent('click', { bubbles: true, cancelable: true }));
    expect(messages).toEqual([{ type: 'openUrl', url: 'https://www.openstreetmap.org/copyright' }]);
    dom.window.close();
  });

  it('bozuk komutları yok sayar', () => {
    const { page, dom } = loadPage();
    expect(() => page.__scootly.receive('{bozuk')).not.toThrow();
    expect(() => page.__scootly.receive('null')).not.toThrow();
    expect(() => page.__scootly.receive('{"type":"center","lat":"x"}')).not.toThrow();
    dom.window.close();
  });

  it('harita kurulamazsa (WebGL yok) bunu sayfada ve uygulamaya söyler', () => {
    FakeMap.fail = true;
    const { page, messages, dom } = loadPage();
    expect(page.document.querySelector('.unsupported')?.textContent).toBe('Harita bu cihazda açılamadı.');
    expect(messages).toContainEqual({ type: 'error', message: 'Harita kurulamadı: WebGL yok' });
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

  it('stil adresindeki </script> sayfayı bozmaz', () => {
    const html = buildMapHtml(
      {
        center: [41, 29],
        zoom: 13,
        focusZoom: 15,
        styleUrl: '</script><script>window.kotu = true</script>',
        scheme: 'light',
        palettes: mapPalettes,
      },
      { embedLibrary: false },
    );
    expect(html).not.toContain('</script><script>window.kotu');
  });
});
