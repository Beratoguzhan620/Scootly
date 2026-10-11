#!/usr/bin/env node
/**
 * Leaflet'i harita sayfasına gömülecek metinlere çevirir: `npm run embed:leaflet`
 *
 * Harita, paneldeki gibi Leaflet + OpenStreetMap ile bir WebView içinde çizilir. Leaflet dosyaları uygulamanın
 * içine gömülür; böylece bir CDN'e bağımlı kalınmaz. Sürüm panelle aynı tutulur (Scootly.Mvc/wwwroot/lib/leaflet).
 */
import { readFileSync, writeFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';

const require = createRequire(import.meta.url);
const packageJsonPath = require.resolve('leaflet/package.json');
const { version } = JSON.parse(readFileSync(packageJsonPath, 'utf8'));
const dist = (file) => readFileSync(join(dirname(packageJsonPath), 'dist', file), 'utf8');

// `</script` sayfadaki <script> bloğunu erken kapatmasın.
const js = dist('leaflet.js').replace(/<\/script/gi, '<\\/script');
// Varsayılan işaret ve katman simgeleri kullanılmıyor; sayfanın adresine göreli resim isteği gitmesin.
const css = dist('leaflet.css').replace(/url\(images\/[^)]+\)/g, 'none');

const output = `// Bu dosya scripts/embed-leaflet.mjs ile üretildi; elle düzenlemeyin.
// Leaflet ${version} — BSD-2-Clause lisanslı. (c) 2010-2023 Vladimir Agafonkin, (c) 2010-2011 CloudMade

export const LEAFLET_VERSION = ${JSON.stringify(version)};

export const LEAFLET_CSS = ${JSON.stringify(css)};

export const LEAFLET_JS = ${JSON.stringify(js)};
`;

const target = new URL('../src/features/map/leafletAssets.generated.ts', import.meta.url);
writeFileSync(target, output);
console.log(`Leaflet ${version} gömüldü → ${target.pathname} (${Math.round(output.length / 1024)} KB)`);
