import { MAP_NUMERAL_FONT_TTF_BASE64 } from './mapFont.generated';
import type { MapPalette } from './mapTheme';
import { MAPLIBRE_CSS, MAPLIBRE_JS } from './maplibreAssets.generated';
import type { Scheme } from '@/theme';

export type MapPageOptions = {
  center: [number, number];
  zoom: number;
  focusZoom: number;
  /** MapLibre stil adresi; boşsa yalnızca düz zemin (testler ve çevrimdışı) */
  styleUrl: string;
  scheme: Scheme;
  palettes: Record<Scheme, MapPalette>;
};

const LINE_SEPARATOR = new RegExp(String.fromCharCode(0x2028), 'g');
const PARAGRAPH_SEPARATOR = new RegExp(String.fromCharCode(0x2029), 'g');

/** JSON'u <script> içine güvenle koymak için: `</script>` ve satır ayırıcıları kaçışlanır. */
export function toScriptJson(value: unknown): string {
  return JSON.stringify(value)
    .replace(/</g, '\\u003c')
    .replace(LINE_SEPARATOR, '\\u2028')
    .replace(PARAGRAPH_SEPARATOR, '\\u2029');
}

const css = `
@font-face{font-family:ScootlyNum;font-weight:700;font-style:normal;
  src:url(data:font/ttf;base64,${MAP_NUMERAL_FONT_TTF_BASE64}) format("truetype");}
html,body,#map{margin:0;padding:0;width:100%;height:100%;overflow:hidden;}
body{background:var(--map-bg);-webkit-tap-highlight-color:transparent;-webkit-user-select:none;user-select:none;
  -webkit-touch-callout:none;font-family:system-ui,-apple-system,Roboto,"Segoe UI",sans-serif;}
.maplibregl-canvas:focus{outline:none;}
.maplibregl-ctrl-bottom-left{transition:bottom .25s ease;}
.maplibregl-ctrl-bottom-left .maplibregl-ctrl{margin:0 0 12px 12px;}
.maplibregl-ctrl-attrib,.maplibregl-ctrl-attrib.maplibregl-compact{background:var(--attr-bg);color:var(--attr-text);
  font-size:10px;line-height:15px;border-radius:14px;box-shadow:0 1px 4px rgba(11,30,74,.12);}
.maplibregl-ctrl-attrib.maplibregl-compact{min-height:28px;}
.maplibregl-ctrl-attrib.maplibregl-compact-show{padding:6px 32px 6px 10px;}
.maplibregl-ctrl-attrib a{color:var(--attr-link);}
.dark .maplibregl-ctrl-attrib-button{filter:invert(1);}
.unsupported{position:absolute;inset:0;display:flex;align-items:center;justify-content:center;padding:32px;
  text-align:center;color:var(--attr-text);font-size:15px;line-height:22px;}
`;

/**
 * Sayfadaki betik: haritayı kurar, uygulamadan gelen durumu çizer, dokunuşları uygulamaya bildirir.
 * Mesaj biçimleri `mapBridge.ts` ile aynı. Sayfa yalnızca bu dizgeden ibaret; Hermes işlev kaynağını
 * saklamadığı için betik ayrı bir işlev olarak yazılamıyor.
 */
const bridge = `
(function () {
  'use strict';
  var cfg = window.__SCOOTLY_MAP__;
  var lib = window.maplibregl;
  var scheme = cfg.scheme === 'dark' ? 'dark' : 'light';
  var dpr = Math.min(3, Math.max(1, window.devicePixelRatio || 1));
  var reduceMotion = !!(window.matchMedia && window.matchMedia('(prefers-reduced-motion: reduce)').matches);

  function send(message) {
    var data = JSON.stringify(message);
    if (window.ReactNativeWebView) window.ReactNativeWebView.postMessage(data);
    else if (window.parent && window.parent !== window) window.parent.postMessage(data, '*');
  }

  window.onerror = function (message) {
    send({ type: 'error', message: String(message) });
  };

  function palette(name) {
    return cfg.palettes[name || scheme];
  }

  // ---------------------------------------------------------------- altlık stilini yeniden boyama

  function fillColor(id, sourceLayer, p) {
    if (sourceLayer === 'water') return p.water;
    if (sourceLayer === 'park' || /park/.test(id)) return p.park;
    if (sourceLayer === 'landcover') return /wood|forest|grass|wetland/.test(id) ? p.wood : p.land;
    if (sourceLayer === 'landuse' || sourceLayer === 'aeroway') return p.residential;
    if (sourceLayer === 'building') return p.building;
    if (sourceLayer === 'transportation') return p.land;
    return null;
  }

  function lineColor(id, sourceLayer, p) {
    if (sourceLayer === 'waterway') return p.water;
    if (sourceLayer === 'boundary') return p.boundary;
    if (sourceLayer === 'aeroway') return p.roadMinor;
    if (sourceLayer !== 'transportation') return null;
    if (/pier|dashline/.test(id)) return p.land;
    if (/rail/.test(id)) return p.rail;
    if (/casing/.test(id)) return p.roadCasing;
    if (/path/.test(id)) return p.path;
    if (/subtle/.test(id)) return p.roadMinor;
    if (/motorway/.test(id)) return p.motorway;
    if (/major/.test(id)) return p.road;
    return p.roadMinor;
  }

  function labelColor(sourceLayer, p) {
    if (sourceLayer === 'water_name' || sourceLayer === 'waterway') return p.waterLabel;
    if (sourceLayer === 'transportation_name' || sourceLayer === 'aerodrome_label') return p.roadLabel;
    if (sourceLayer === 'place') return p.placeLabel;
    return null;
  }

  // OpenMapTiles şemasındaki katmanları uygulamanın renklerine boyar; tanımadığı katmana dokunmaz.
  function recolor(style, p) {
    var layers = [];
    for (var i = 0; i < (style.layers || []).length; i++) {
      var source = style.layers[i];
      var layer = Object.assign({}, source);
      var id = String(layer.id || '');
      var sourceLayer = layer['source-layer'] || '';
      var paint = Object.assign({}, layer.paint || {});
      if (/shield/.test(id)) layer.layout = Object.assign({}, layer.layout || {}, { visibility: 'none' });
      if (layer.type === 'background') {
        paint['background-color'] = p.land;
      } else if (layer.type === 'fill') {
        var fill = fillColor(id, sourceLayer, p);
        if (fill) {
          paint['fill-color'] = fill;
          if ('fill-outline-color' in paint) paint['fill-outline-color'] = sourceLayer === 'building' ? p.buildingLine : fill;
        }
      } else if (layer.type === 'line') {
        var line = lineColor(id, sourceLayer, p);
        if (line) paint['line-color'] = line;
      } else if (layer.type === 'symbol') {
        var text = labelColor(sourceLayer, p);
        if (text) {
          paint['text-color'] = text;
          paint['text-halo-color'] = p.halo;
        }
      }
      layer.paint = paint;
      layers.push(layer);
    }
    return Object.assign({}, style, { layers: layers });
  }

  function fallbackStyle() {
    return {
      version: 8,
      sources: {},
      layers: [{ id: 'background', type: 'background', paint: { 'background-color': palette().background } }]
    };
  }

  // ---------------------------------------------------------------- işaret resimleri

  function surface(width, height) {
    var canvas = document.createElement('canvas');
    canvas.width = Math.ceil(width * dpr);
    canvas.height = Math.ceil(height * dpr);
    var ctx = canvas.getContext && canvas.getContext('2d');
    if (!ctx) return null;
    ctx.scale(dpr, dpr);
    return { canvas: canvas, ctx: ctx };
  }

  function circle(ctx, x, y, r) {
    ctx.beginPath();
    ctx.arc(x, y, r, 0, Math.PI * 2);
  }

  function numberFont(size) {
    return '700 ' + size + 'px ScootlyNum, "Roboto Condensed", "Arial Narrow", sans-serif';
  }

  // Sarı levha: beyaz kenar, içte lacivert batarya yayı, ortada yüzde. Seçili olan büyür ve bir direğe oturur.
  var PIN = { r: 19, ring: 3.5, pad: 6 };
  var SELECTED = { r: 26, ring: 4.5, pad: 6, stem: 14, below: 6 };

  // status: a müsait (sarı levha) · r/i/m/l filo haritasında durum rengi
  function drawPin(battery, level, status, selected, p) {
    var st = status && status !== 'a' && p.status ? p.status[status] : null;
    var g = selected ? SELECTED : PIN;
    var stem = selected ? g.stem : 0;
    var width = g.r * 2 + g.pad * 2;
    var height = g.r * 2 + g.pad * 2 + (selected ? stem + g.below - g.pad : 0);
    var s = surface(width, height);
    if (!s) return null;
    var ctx = s.ctx;
    var cx = width / 2;
    var cy = g.pad + g.r;
    var off = !st && level === 'unrentable';

    if (selected) {
      ctx.fillStyle = p.ring;
      ctx.fillRect(cx - 2, cy + g.r - 4, 4, stem + 4);
      circle(ctx, cx, cy + g.r + stem, 4);
      ctx.fill();
      ctx.lineWidth = 2;
      ctx.strokeStyle = p.border;
      ctx.stroke();
    }

    ctx.save();
    ctx.shadowColor = p.shadow;
    ctx.shadowBlur = selected ? 10 : 6;
    ctx.shadowOffsetY = 2;
    circle(ctx, cx, cy, g.r);
    ctx.fillStyle = selected ? p.ring : p.border;
    ctx.fill();
    ctx.restore();

    var face = g.r - (selected ? 3 : 2);
    circle(ctx, cx, cy, face);
    ctx.fillStyle = off ? p.offFill : st ? st.fill : p.fill;
    ctx.fill();

    var ringRadius = face - g.ring / 2 - 2.5;
    circle(ctx, cx, cy, ringRadius);
    ctx.lineWidth = g.ring;
    ctx.strokeStyle = off ? 'rgba(85,98,127,0.25)' : st ? st.track : 'rgba(11,30,74,0.16)';
    ctx.stroke();
    if (battery > 0) {
      ctx.beginPath();
      ctx.arc(cx, cy, ringRadius, -Math.PI / 2, -Math.PI / 2 + (Math.PI * 2 * Math.min(100, battery)) / 100);
      ctx.lineCap = 'round';
      ctx.strokeStyle = st ? st.ring : level === 'ok' ? p.ring : p.low;
      ctx.stroke();
    }

    var size = selected ? (battery >= 100 ? 18 : 21) : battery >= 100 ? 12 : 15;
    ctx.font = numberFont(size);
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillStyle = off ? p.offText : st ? st.text : p.text;
    ctx.fillText(String(battery), cx, cy + size * 0.05);
    return ctx.getImageData(0, 0, s.canvas.width, s.canvas.height);
  }

  function clusterRadius(count) {
    return count < 10 ? 19 : count < 50 ? 22 : count < 100 ? 25 : 28;
  }

  function drawCluster(count, p) {
    var r = clusterRadius(count);
    var halo = 6;
    var width = (r + halo) * 2 + 4;
    var s = surface(width, width);
    if (!s) return null;
    var ctx = s.ctx;
    var c = width / 2;
    circle(ctx, c, c, r + halo);
    ctx.fillStyle = p.cluster.halo;
    ctx.fill();
    circle(ctx, c, c, r);
    ctx.fillStyle = p.cluster.border;
    ctx.fill();
    circle(ctx, c, c, r - 2);
    ctx.fillStyle = p.cluster.fill;
    ctx.fill();
    var label = count > 999 ? '999+' : String(count);
    var size = label.length > 3 ? 13 : label.length > 2 ? 15 : 17;
    ctx.font = numberFont(size);
    ctx.textAlign = 'center';
    ctx.textBaseline = 'middle';
    ctx.fillStyle = p.cluster.text;
    ctx.fillText(label, c, c + size * 0.05);
    return ctx.getImageData(0, 0, s.canvas.width, s.canvas.height);
  }

  // Resim adları: "p|86|ok|a|light" (işaret), "s|86|ok|a|light" (seçili), "c|12|light" (küme)
  function addIcon(id) {
    if (!map || map.hasImage(id)) return;
    var parts = String(id).split('|');
    var p = cfg.palettes[parts[parts.length - 1]];
    if (!p) return;
    var image = null;
    if (parts[0] === 'p' || parts[0] === 's') {
      image = drawPin(Number(parts[1]) || 0, parts[2], parts[3], parts[0] === 's', p.pin);
    }
    else if (parts[0] === 'c') image = drawCluster(Number(parts[1]) || 0, p);
    if (image) map.addImage(id, image, { pixelRatio: dpr });
  }

  // ---------------------------------------------------------------- durum

  var state = { vehicles: [], areas: [], user: null, bottomInset: 0 };
  var selectedId = null;
  var baseStyle = null;
  var styleAttempts = 0;
  var overlayReady = false;
  var lastData = {};

  function feature(lng, lat, properties) {
    return { type: 'Feature', geometry: { type: 'Point', coordinates: [lng, lat] }, properties: properties };
  }

  function collection(features) {
    return { type: 'FeatureCollection', features: features };
  }

  function iconFor(vehicle, selected) {
    return (selected ? 's|' : 'p|') + vehicle.battery + '|' + vehicle.level + '|' + (vehicle.status || 'a') + '|' + scheme;
  }

  function vehiclesData() {
    var features = [];
    for (var i = 0; i < state.vehicles.length; i++) {
      var v = state.vehicles[i];
      if (v.selected) continue;
      features.push(feature(v.lng, v.lat, { id: v.id, icon: iconFor(v, false), sort: -v.lat }));
    }
    return collection(features);
  }

  function selectedVehicle() {
    for (var i = 0; i < state.vehicles.length; i++) if (state.vehicles[i].selected) return state.vehicles[i];
    return null;
  }

  function selectedData() {
    var v = selectedVehicle();
    return collection(v ? [feature(v.lng, v.lat, { id: v.id, icon: iconFor(v, true) })] : []);
  }

  function areasData() {
    var features = [];
    for (var i = 0; i < state.areas.length; i++) {
      var points = state.areas[i].points || [];
      if (points.length < 3) continue;
      var ring = [];
      for (var j = 0; j < points.length; j++) ring.push([points[j][1], points[j][0]]);
      ring.push(ring[0]);
      features.push({ type: 'Feature', geometry: { type: 'Polygon', coordinates: [ring] }, properties: { name: state.areas[i].name } });
    }
    return collection(features);
  }

  function userData() {
    return collection(state.user ? [feature(state.user.lng, state.user.lat, {})] : []);
  }

  function setData(id, data) {
    var key = JSON.stringify(data);
    if (lastData[id] === key) return;
    var source = map.getSource(id);
    if (!source) return;
    lastData[id] = key;
    source.setData(data);
  }

  function updateSources() {
    if (!overlayReady) return;
    setData('areas', areasData());
    setData('user', userData());
    setData('vehicles', vehiclesData());
    setData('selected', selectedData());
  }

  function firstSymbolLayerId() {
    var layers = (map.getStyle() && map.getStyle().layers) || [];
    for (var i = 0; i < layers.length; i++) if (layers[i].type === 'symbol') return layers[i].id;
    return undefined;
  }

  // Stil her yüklendiğinde (açılış, tema değişimi) uygulamanın katmanları yeniden eklenir.
  function addOverlay() {
    var p = palette();
    var before = firstSymbolLayerId();
    lastData = {};
    map.addSource('areas', { type: 'geojson', data: areasData() });
    map.addLayer({ id: 'areas-fill', type: 'fill', source: 'areas',
      paint: { 'fill-color': p.area.fill, 'fill-opacity': p.area.fillOpacity } }, before);
    map.addLayer({ id: 'areas-line', type: 'line', source: 'areas', layout: { 'line-join': 'round' },
      paint: { 'line-color': p.area.line, 'line-opacity': p.area.lineOpacity, 'line-width': 2, 'line-dasharray': [2, 1.5] } }, before);
    map.addSource('user', { type: 'geojson', data: userData() });
    map.addLayer({ id: 'user-halo', type: 'circle', source: 'user',
      paint: { 'circle-radius': 18, 'circle-color': p.user.dot, 'circle-opacity': 0.16 } });
    map.addLayer({ id: 'user-dot', type: 'circle', source: 'user',
      paint: { 'circle-radius': 7, 'circle-color': p.user.dot, 'circle-stroke-color': p.user.border, 'circle-stroke-width': 3 } });
    map.addSource('vehicles', { type: 'geojson', data: vehiclesData(), cluster: true, clusterRadius: 46, clusterMaxZoom: 16 });
    map.addLayer({ id: 'clusters', type: 'symbol', source: 'vehicles', filter: ['has', 'point_count'],
      layout: { 'icon-image': ['concat', 'c|', ['to-string', ['get', 'point_count']], '|', scheme],
        'icon-allow-overlap': true, 'icon-ignore-placement': true } });
    map.addLayer({ id: 'pins', type: 'symbol', source: 'vehicles', filter: ['!', ['has', 'point_count']],
      layout: { 'icon-image': ['get', 'icon'], 'icon-allow-overlap': true, 'icon-ignore-placement': true,
        'symbol-sort-key': ['get', 'sort'] } });
    map.addSource('selected', { type: 'geojson', data: selectedData() });
    map.addLayer({ id: 'selected', type: 'symbol', source: 'selected',
      layout: { 'icon-image': ['get', 'icon'], 'icon-anchor': 'bottom', 'icon-offset': [0, SELECTED.below],
        'icon-allow-overlap': true, 'icon-ignore-placement': true } });
    overlayReady = true;
  }

  function applyStyle() {
    overlayReady = false;
    map.setStyle(baseStyle ? recolor(baseStyle, palette().base) : fallbackStyle(), { diff: false });
  }

  // Stil alınamazsa (bağlantı yok) araçlar düz zeminde görünmeye devam eder; artan aralıklarla yeniden denenir.
  function loadBaseStyle() {
    if (!cfg.styleUrl || typeof fetch !== 'function') return;
    fetch(cfg.styleUrl, { credentials: 'omit' })
      .then(function (response) {
        if (!response.ok) throw new Error('HTTP ' + response.status);
        return response.json();
      })
      .then(function (style) {
        baseStyle = style;
        applyStyle();
      })
      .catch(function (error) {
        send({ type: 'error', message: 'Harita stili alınamadı: ' + (error && error.message) });
        styleAttempts += 1;
        setTimeout(loadBaseStyle, Math.min(60000, 3000 * Math.pow(2, styleAttempts - 1)));
      });
  }

  function applyThemeChrome() {
    var p = palette();
    var root = document.documentElement;
    root.style.setProperty('--map-bg', p.background);
    root.style.setProperty('--attr-bg', p.attribution.bg);
    root.style.setProperty('--attr-text', p.attribution.text);
    root.style.setProperty('--attr-link', p.attribution.link);
    root.className = scheme === 'dark' ? 'dark' : '';
  }

  // ---------------------------------------------------------------- kamera

  function containerSize() {
    var container = map.getContainer();
    return { width: container.clientWidth || 1, height: container.clientHeight || 1 };
  }

  // Altta panel varken yalnızca görünen kısmı bildir: sayılar ve sorgu, kullanıcının gördüğü alanla aynı olsun.
  function visibleRegion() {
    var size = containerSize();
    var visibleHeight = Math.max(1, size.height - state.bottomInset);
    var northWest = map.unproject([0, 0]);
    var southEast = map.unproject([size.width, visibleHeight]);
    return {
      latitude: (northWest.lat + southEast.lat) / 2,
      longitude: (northWest.lng + southEast.lng) / 2,
      latitudeDelta: Math.abs(northWest.lat - southEast.lat),
      longitudeDelta: Math.abs(southEast.lng - northWest.lng)
    };
  }

  function reportRegion() {
    send({ type: 'region', region: visibleRegion() });
  }

  function duration(ms) {
    return reduceMotion ? 0 : ms;
  }

  // Noktayı panelin üstünde kalan alanın ortasına getir.
  function centerOn(lat, lng, zoom) {
    var target = typeof zoom === 'number' ? zoom : Math.max(map.getZoom(), cfg.focusZoom);
    map.easeTo({ center: [lng, lat], zoom: target, offset: [0, -state.bottomInset / 2], duration: duration(700) });
  }

  // Seçili araç panelin altında ya da ekran dışında kaldıysa görünen alanın ortasına kaydır (yakınlık korunur).
  function ensureSelectedVisible() {
    var v = selectedVehicle();
    if (!v) return;
    var size = containerSize();
    var point = map.project([v.lng, v.lat]);
    if (point.x < 40 || point.x > size.width - 40 || point.y < 96 || point.y > size.height - state.bottomInset - 24) {
      map.easeTo({ center: [v.lng, v.lat], offset: [0, -state.bottomInset / 2 + 24], duration: duration(450) });
    }
  }

  // Seçilen işaret küçükten büyüyerek "oturur".
  function popSelected() {
    if (reduceMotion || !overlayReady || !map.getLayer('selected') || !window.requestAnimationFrame) return;
    var start = null;
    function frame(time) {
      if (!map.getLayer('selected')) return;
      if (start === null) start = time;
      var t = Math.min(1, (time - start) / 280);
      var back = 1 + 2.70158 * Math.pow(t - 1, 3) + 1.70158 * Math.pow(t - 1, 2);
      map.setLayoutProperty('selected', 'icon-size', 0.5 + 0.5 * back);
      if (t < 1) window.requestAnimationFrame(frame);
    }
    window.requestAnimationFrame(frame);
  }

  // Atıf, harita açılınca açık görünür; kullanıcı haritayla ilk etkileşime girince "i" düğmesine küçülür.
  function collapseAttribution() {
    var attribution = document.querySelector('.maplibregl-ctrl-attrib.maplibregl-compact-show');
    if (attribution) attribution.classList.remove('maplibregl-compact-show');
  }

  function positionControls() {
    var corner = document.querySelector('.maplibregl-ctrl-bottom-left');
    if (corner) corner.style.bottom = state.bottomInset + 'px';
  }

  // ---------------------------------------------------------------- dokunuş

  function zoomToCluster(target) {
    var source = map.getSource('vehicles');
    var coordinates = target.geometry.coordinates;
    function go(zoom) {
      map.easeTo({ center: coordinates, zoom: Math.min(18, zoom + 0.5), offset: [0, -state.bottomInset / 2], duration: duration(500) });
    }
    try {
      var result = source.getClusterExpansionZoom(target.properties.cluster_id);
      if (result && typeof result.then === 'function') result.then(go, function () { go(map.getZoom() + 2); });
      else go(map.getZoom() + 2);
    } catch (error) {
      go(map.getZoom() + 2);
    }
  }

  // Parmak ucu işaretten biraz şaşsa da seçilsin: dokunulan noktanın çevresindeki en yakın işaret.
  function onClick(event) {
    collapseAttribution();
    var hits = overlayReady
      ? map.queryRenderedFeatures(
          [[event.point.x - 20, event.point.y - 20], [event.point.x + 20, event.point.y + 20]],
          { layers: ['selected', 'pins', 'clusters'] })
      : [];
    var best = null;
    var bestDistance = Infinity;
    for (var i = 0; i < hits.length; i++) {
      var hit = hits[i];
      var coordinates = hit.geometry && hit.geometry.coordinates;
      if (!coordinates) continue;
      var point = map.project(coordinates);
      var dx = point.x - event.point.x;
      var dy = point.y - (hit.layer.id === 'selected' ? SELECTED.r + SELECTED.stem : 0) - event.point.y;
      var distance = Math.sqrt(dx * dx + dy * dy);
      if (distance < bestDistance) {
        best = hit;
        bestDistance = distance;
      }
    }
    if (!best) {
      send({ type: 'mapPress' });
    } else if (best.layer.id === 'clusters') {
      zoomToCluster(best);
    } else if (best.layer.id === 'pins' && best.properties && best.properties.id) {
      send({ type: 'select', id: String(best.properties.id) });
    }
  }

  // ---------------------------------------------------------------- uygulamadan gelen komutlar

  function setState(command) {
    var previousSelected = selectedId;
    var inset = Math.max(0, Number(command.bottomInset) || 0);
    var insetChanged = inset !== state.bottomInset;
    state.vehicles = Array.isArray(command.vehicles) ? command.vehicles : [];
    state.areas = Array.isArray(command.areas) ? command.areas : [];
    state.user = command.user || null;
    state.bottomInset = inset;
    var selected = selectedVehicle();
    selectedId = selected ? selected.id : null;
    updateSources();
    if (insetChanged) positionControls();
    if (selectedId && selectedId !== previousSelected) {
      collapseAttribution();
      popSelected();
    }
    if (selectedId && (selectedId !== previousSelected || insetChanged)) ensureSelectedVisible();
    if (insetChanged) reportRegion();
  }

  function setScheme(next) {
    next = next === 'dark' ? 'dark' : 'light';
    if (next === scheme) return;
    scheme = next;
    applyThemeChrome();
    applyStyle();
  }

  function receive(json) {
    var command;
    try {
      command = typeof json === 'string' ? JSON.parse(json) : json;
    } catch (error) {
      return;
    }
    if (!command || typeof command !== 'object') return;
    if (command.type === 'state') setState(command);
    else if (command.type === 'center') centerOn(Number(command.lat), Number(command.lng), command.zoom);
    else if (command.type === 'theme') setScheme(command.scheme);
  }

  // ---------------------------------------------------------------- kurulum

  applyThemeChrome();

  var map = null;
  try {
    map = new lib.Map({
      container: 'map',
      style: fallbackStyle(),
      center: [cfg.center[1], cfg.center[0]],
      zoom: cfg.zoom,
      minZoom: 3,
      maxZoom: 19,
      attributionControl: false,
      dragRotate: false,
      pitchWithRotate: false,
      touchPitch: false,
      keyboard: false,
      boxZoom: false,
      fadeDuration: 150,
      clickTolerance: 6
    });
  } catch (error) {
    var notice = document.createElement('div');
    notice.className = 'unsupported';
    notice.textContent = 'Harita bu cihazda açılamadı.';
    document.body.appendChild(notice);
    send({ type: 'error', message: 'Harita kurulamadı: ' + (error && error.message) });
    return;
  }

  if (map.touchZoomRotate && map.touchZoomRotate.disableRotation) map.touchZoomRotate.disableRotation();
  map.addControl(new lib.AttributionControl({ compact: true }), 'bottom-left');

  map.on('styleimagemissing', function (event) { addIcon(event.id); });
  map.on('style.load', addOverlay);
  map.on('moveend', reportRegion);
  map.on('click', onClick);
  map.on('error', function (event) {
    var error = event && event.error;
    send({ type: 'error', message: String((error && error.message) || error || 'Harita hatası') });
  });

  // Atıf bağlantıları sayfanın içinde değil, sistem tarayıcısında açılır.
  document.addEventListener('click', function (event) {
    var target = event.target;
    while (target && target.nodeName !== 'A') target = target.parentNode;
    if (target && target.href) {
      event.preventDefault();
      event.stopPropagation();
      send({ type: 'openUrl', url: target.href });
    }
  }, true);

  window.__scootly = { receive: receive };
  window.addEventListener('message', function (event) {
    if (typeof event.data === 'string') receive(event.data);
  });

  // İşaretlerdeki rakamlar gömülü yazı tipiyle çizilsin diye önce yazı tipi yüklenir (en fazla 1,5 sn beklenir).
  var fontReady = document.fonts && document.fonts.load
    ? Promise.race([document.fonts.load(numberFont(16)), new Promise(function (resolve) { setTimeout(resolve, 1500); })])
    : Promise.resolve();
  fontReady.then(loadBaseStyle, loadBaseStyle);

  send({ type: 'ready' });
  reportRegion();
})();
`;

export type MapPageBuildOptions = {
  /** Testlerde MapLibre yerine sahte bir kütüphane enjekte edilir. */
  embedLibrary?: boolean;
};

/** MapLibre ve köprü betiği gömülü, kendi kendine yeten harita sayfası. */
export function buildMapHtml(options: MapPageOptions, { embedLibrary = true }: MapPageBuildOptions = {}): string {
  const config = toScriptJson(options);
  const background = options.palettes[options.scheme].background;

  return `<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
<meta name="referrer" content="no-referrer-when-downgrade">
<title>Araç haritası</title>
${embedLibrary ? `<style>${MAPLIBRE_CSS}</style>` : ''}
<style>:root{--map-bg:${background};}${css}</style>
</head>
<body>
<div id="map" role="application" aria-label="Araç haritası"></div>
${embedLibrary ? `<script>${MAPLIBRE_JS}</script>` : ''}
<script>window.__SCOOTLY_MAP__ = ${config};</script>
<script>${bridge}</script>
</body>
</html>`;
}
