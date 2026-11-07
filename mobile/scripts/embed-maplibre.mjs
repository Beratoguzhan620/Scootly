#!/usr/bin/env node
/**
 * MapLibre GL JS'i harita sayfasına gömülecek metinlere çevirir: `npm run embed:maplibre`
 *
 * Harita, vektör karolarla MapLibre GL JS tarafından bir WebView içinde çizilir. Kütüphane uygulamanın içine
 * gömülür; böylece bir CDN'e bağımlı kalınmaz ve sayfa her açılışta aynı sürümle çalışır. Tek dosyalık UMD
 * paketi kullanılır (5.x); 6.x yalnızca ES modülü ve ayrı işçi (worker) dosyası olarak geliyor.
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';

const require = createRequire(import.meta.url);
const packageJsonPath = require.resolve('maplibre-gl/package.json');
const { version, license } = JSON.parse(readFileSync(packageJsonPath, 'utf8'));
const dist = (file) => readFileSync(join(dirname(packageJsonPath), 'dist', file), 'utf8');

// `</script` sayfadaki <script> bloğunu erken kapatmasın; kaynak haritası yorumu da gereksiz.
const js = dist('maplibre-gl.js')
  .replace(/<\/script/gi, '<\\/script')
  .replace(/\n\/\/# sourceMappingURL=.*$/m, '');
const css = dist('maplibre-gl.css').replace(/\/\*# sourceMappingURL=.*?\*\//g, '');

const output = `// Bu dosya scripts/embed-maplibre.mjs ile üretildi; elle düzenlemeyin.
// MapLibre GL JS ${version} — ${license} lisanslı. https://github.com/maplibre/maplibre-gl-js

export const MAPLIBRE_VERSION = ${JSON.stringify(version)};

export const MAPLIBRE_CSS = ${JSON.stringify(css)};

export const MAPLIBRE_JS = ${JSON.stringify(js)};
`;

const target = new URL('../src/features/map/maplibreAssets.generated.ts', import.meta.url);
writeFileSync(target, output);
console.log(`MapLibre ${version} gömüldü → ${target.pathname} (${Math.round(output.length / 1024)} KB)`);
