/**
 * Harita karoları. Panel (Scootly.Mvc → map.js) OpenStreetMap'in standart karolarını kullanıyor; mobil de aynı
 * görünür. OSM'nin gönüllülerce işletilen sunucuları geliştirme ve küçük çaplı kullanım içindir; uygulama geniş
 * kitleye dağıtılacaksa `EXPO_PUBLIC_MAP_TILE_URL` ile ticari kullanıma izin veren bir sağlayıcıya geçilmeli
 * (https://operations.osmfoundation.org/policies/tiles/).
 */
export const MAP_TILE_URL =
  process.env.EXPO_PUBLIC_MAP_TILE_URL?.trim() || 'https://tile.openstreetmap.org/{z}/{x}/{y}.png';

/** Karolar OSM verisinden üretildiği sürece bu atıf görünür olmalı (lisans gereği). */
export const MAP_ATTRIBUTION =
  process.env.EXPO_PUBLIC_MAP_ATTRIBUTION?.trim() ||
  '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> katkıda bulunanları';

/**
 * Harita sayfasının adresi (WebView `baseUrl`). Karo sunucuları isteğin hangi uygulamadan geldiğini
 * `Referer` başlığından görmek ister; adres projenin sayfası olduğu için uygulamayı tanımlar.
 */
export const MAP_PAGE_URL = 'https://github.com/Beratoguzhan620/Scootly';

/** Konuma gidilirken kullanılan yakınlık: birkaç yüz metre çevresi görünür. */
export const FOCUS_ZOOM = 15;

/** Haritada dokunulunca sistem tarayıcısında açılabilecek bağlantılar (atıf metnindekiler). */
const EXTERNAL_LINK_HOSTS = ['www.openstreetmap.org', 'openstreetmap.org', 'leafletjs.com', 'osmfoundation.org'];

export function isAllowedExternalUrl(url: string): boolean {
  const match = /^https:\/\/([^/?#:]+)(?:[/?#]|$)/i.exec(url.trim());
  return match !== null && EXTERNAL_LINK_HOSTS.includes(match[1].toLowerCase());
}
