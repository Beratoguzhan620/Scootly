import { LEAFLET_CSS, LEAFLET_JS } from './leafletAssets.generated';
import { colors } from '@/theme';

export type MapPageOptions = {
  center: [number, number];
  zoom: number;
  focusZoom: number;
  tileUrl: string;
  attribution: string;
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

/** Paneldeki rozet renkleri; işaretler `VehicleStatusTagHelper` ile aynı tonlarda. */
const pinTones: Record<string, { bg: string; text: string }> = {
  success: { bg: colors.success, text: colors.white },
  warning: { bg: colors.warning, text: colors.dark },
  primary: { bg: colors.primary, text: colors.white },
  secondary: { bg: colors.secondary, text: colors.white },
  danger: { bg: colors.danger, text: colors.white },
  light: { bg: colors.light, text: colors.dark },
};

const pinToneCss = Object.entries(pinTones)
  .map(([tone, palette]) => `.pin-${tone}{background:${palette.bg};color:${palette.text}}`)
  .join('\n');

const css = `
html,body,#map{margin:0;padding:0;width:100%;height:100%;}
body{background:${colors.secondaryBg};-webkit-tap-highlight-color:transparent;-webkit-user-select:none;user-select:none;
  font-family:system-ui,-apple-system,"Segoe UI",Roboto,"Helvetica Neue",Arial,sans-serif;}
.leaflet-container{background:${colors.secondaryBg};font-family:inherit;}
.pin-anchor,.user-anchor{background:none;border:none;}
.pin{position:absolute;left:0;top:0;transform:translate(-50%,-50%);box-sizing:border-box;min-width:44px;height:26px;
  padding:0 8px;border:2px solid ${colors.white};border-radius:13px;display:flex;align-items:center;justify-content:center;
  font-size:12px;font-weight:700;line-height:1;white-space:nowrap;box-shadow:0 1px 3px rgba(0,0,0,.35);cursor:pointer;}
.pin::before{content:"";position:absolute;left:-6px;right:-6px;top:-10px;bottom:-10px;}
.pin-selected{height:32px;border-radius:16px;border:3px solid ${colors.dark};font-size:14px;}
${pinToneCss}
.user-dot{position:absolute;left:0;top:0;transform:translate(-50%,-50%);box-sizing:border-box;width:18px;height:18px;
  border-radius:50%;background:${colors.primary};border:3px solid ${colors.white};
  box-shadow:0 0 0 7px rgba(13,110,253,.18),0 1px 3px rgba(0,0,0,.35);}
.leaflet-control-attribution{margin:8px!important;padding:2px 6px!important;border-radius:4px;font-size:11px;
  background:rgba(255,255,255,.85)!important;color:${colors.body};}
.leaflet-control-attribution a{color:${colors.link};}
`;

/**
 * Sayfadaki köprü betiği. Eski Android WebView'larda da çalışsın diye ES5 ile yazıldı.
 * Mesaj biçimleri `mapBridge.ts` ile aynı.
 */
const bridge = `
(function () {
  var cfg = window.__SCOOTLY_MAP__;
  var TONES = { success: 1, warning: 1, primary: 1, secondary: 1, danger: 1, light: 1 };

  function send(message) {
    var data = JSON.stringify(message);
    if (window.ReactNativeWebView) window.ReactNativeWebView.postMessage(data);
    else if (window.parent && window.parent !== window) window.parent.postMessage(data, '*');
  }

  window.onerror = function (message) {
    send({ type: 'error', message: String(message) });
  };

  var map = L.map('map', {
    center: cfg.center,
    zoom: cfg.zoom,
    minZoom: 3,
    maxZoom: 19,
    zoomControl: false,
    attributionControl: false,
    keyboard: false,
    tapTolerance: 20
  });
  L.control.attribution({ position: 'topleft', prefix: false }).addAttribution(cfg.attribution).addTo(map);
  L.tileLayer(cfg.tileUrl, { maxZoom: 19 }).addTo(map);

  var areasLayer = L.layerGroup().addTo(map);
  var vehiclesLayer = L.layerGroup().addTo(map);
  var markers = {};
  var userMarker = null;
  var areasKey = '';
  var bottomInset = 0;
  var selectedId = null;

  function pinIcon(vehicle) {
    var element = document.createElement('div');
    element.className = 'pin pin-' + (TONES[vehicle.tone] ? vehicle.tone : 'secondary') + (vehicle.selected ? ' pin-selected' : '');
    element.textContent = '%' + vehicle.battery;
    element.setAttribute('role', 'button');
    element.setAttribute('aria-label', vehicle.label);
    return L.divIcon({ className: 'pin-anchor', html: element, iconSize: [0, 0], iconAnchor: [0, 0] });
  }

  function setVehicles(list) {
    var seen = {};
    var nextSelected = null;
    for (var i = 0; i < list.length; i++) {
      var vehicle = list[i];
      if (vehicle.selected) nextSelected = vehicle.id;
      var key = vehicle.tone + '|' + vehicle.battery + '|' + (vehicle.selected ? 1 : 0) + '|' + vehicle.label;
      var entry = markers[vehicle.id];
      seen[vehicle.id] = true;
      if (!entry) {
        var marker = L.marker([vehicle.lat, vehicle.lng], {
          icon: pinIcon(vehicle),
          keyboard: false,
          zIndexOffset: vehicle.selected ? 1000 : 0
        });
        marker.on('click', (function (id) {
          return function () { send({ type: 'select', id: id }); };
        })(vehicle.id));
        marker.addTo(vehiclesLayer);
        markers[vehicle.id] = { marker: marker, key: key };
      } else {
        entry.marker.setLatLng([vehicle.lat, vehicle.lng]);
        if (entry.key !== key) {
          entry.marker.setIcon(pinIcon(vehicle));
          entry.marker.setZIndexOffset(vehicle.selected ? 1000 : 0);
          entry.key = key;
        }
      }
    }
    for (var id in markers) {
      if (Object.prototype.hasOwnProperty.call(markers, id) && !seen[id]) {
        vehiclesLayer.removeLayer(markers[id].marker);
        delete markers[id];
      }
    }
    var selectionChanged = nextSelected !== selectedId;
    selectedId = nextSelected;
    return selectionChanged;
  }

  // Seçili araç kartın altında ya da ekran dışında kaldıysa görünen alanın ortasına kaydır (yakınlık korunur).
  function ensureSelectedVisible() {
    var entry = selectedId && markers[selectedId];
    if (!entry) return;
    var size = map.getSize();
    var point = map.latLngToContainerPoint(entry.marker.getLatLng());
    var margin = 48;
    if (point.x < margin || point.x > size.x - margin || point.y < margin || point.y > size.y - bottomInset - margin) {
      var zoom = map.getZoom();
      var target = map.project(entry.marker.getLatLng(), zoom).add([0, bottomInset / 2]);
      map.panTo(map.unproject(target, zoom));
    }
  }

  function setAreas(areas) {
    var key = JSON.stringify(areas);
    if (key === areasKey) return;
    areasKey = key;
    areasLayer.clearLayers();
    for (var i = 0; i < areas.length; i++) {
      L.polygon(areas[i].points, {
        color: cfg.areaColor,
        weight: 2,
        fillColor: cfg.areaColor,
        fillOpacity: 0.06,
        interactive: false
      }).addTo(areasLayer);
    }
  }

  function setUser(user) {
    if (!user) {
      if (userMarker) {
        map.removeLayer(userMarker);
        userMarker = null;
      }
      return;
    }
    if (userMarker) {
      userMarker.setLatLng([user.lat, user.lng]);
      return;
    }
    userMarker = L.marker([user.lat, user.lng], {
      icon: L.divIcon({ className: 'user-anchor', html: '<div class="user-dot"></div>', iconSize: [0, 0], iconAnchor: [0, 0] }),
      interactive: false,
      keyboard: false,
      zIndexOffset: -1000
    }).addTo(map);
  }

  // Altta kart varken yalnızca görünen kısmı bildir: sayılar ve sorgu, kullanıcının gördüğü alanla aynı olsun.
  function visibleRegion() {
    var size = map.getSize();
    var visibleHeight = Math.max(1, size.y - bottomInset);
    var northWest = map.containerPointToLatLng([0, 0]);
    var southEast = map.containerPointToLatLng([size.x, visibleHeight]);
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

  // Noktayı kartın üstünde kalan alanın ortasına getir.
  function centerOn(lat, lng, zoom) {
    var targetZoom = zoom || Math.max(map.getZoom(), cfg.focusZoom);
    var point = map.project([lat, lng], targetZoom).add([0, bottomInset / 2]);
    map.setView(map.unproject(point, targetZoom), targetZoom);
  }

  function receive(json) {
    var command;
    try {
      command = typeof json === 'string' ? JSON.parse(json) : json;
    } catch (error) {
      return;
    }
    if (!command) return;
    if (command.type === 'state') {
      var insetChanged = (command.bottomInset || 0) !== bottomInset;
      bottomInset = command.bottomInset || 0;
      setAreas(command.areas || []);
      var selectionChanged = setVehicles(command.vehicles || []);
      setUser(command.user || null);
      if (selectionChanged || insetChanged) ensureSelectedVisible();
      if (insetChanged) reportRegion();
    } else if (command.type === 'center') {
      centerOn(command.lat, command.lng, command.zoom);
    }
  }

  map.on('moveend', reportRegion);
  map.on('click', function () { send({ type: 'mapPress' }); });

  // Atıf bağlantıları WebView'ın içinde değil, sistem tarayıcısında açılır.
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

  send({ type: 'ready' });
  reportRegion();
})();
`;

/** Leaflet ve köprü betiği gömülü, kendi kendine yeten harita sayfası. */
export function buildMapHtml(options: MapPageOptions): string {
  const config = toScriptJson({ ...options, areaColor: colors.primary });

  return `<!DOCTYPE html>
<html lang="tr">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1, maximum-scale=1, user-scalable=no">
<meta name="referrer" content="no-referrer-when-downgrade">
<title>Araç haritası</title>
<style>${LEAFLET_CSS}</style>
<style>${css}</style>
</head>
<body>
<div id="map" role="application" aria-label="Araç haritası"></div>
<script>${LEAFLET_JS}</script>
<script>window.__SCOOTLY_MAP__ = ${config};</script>
<script>${bridge}</script>
</body>
</html>`;
}
