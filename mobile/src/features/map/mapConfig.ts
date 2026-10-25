/**
 * Harita altlığı: OpenFreeMap'in vektör karoları ve "Positron" stili. API anahtarı ya da kullanım sınırı
 * yoktur (https://openfreemap.org). Stil uygulamanın renklerine sayfada yeniden boyanır; açık ve koyu tema
 * aynı stilden üretilir. Başka bir MapLibre stiline geçmek için `EXPO_PUBLIC_MAP_STYLE_URL`.
 */
export const MAP_STYLE_URL =
  process.env.EXPO_PUBLIC_MAP_STYLE_URL?.trim() || 'https://tiles.openfreemap.org/styles/positron';

/**
 * Harita sayfasının adresi (WebView `baseUrl`). Karo sunucuları isteğin hangi uygulamadan geldiğini
 * `Referer` başlığından görebilir; adres projenin sayfası olduğu için uygulamayı tanımlar.
 */
export const MAP_PAGE_URL = 'https://github.com/Beratoguzhan620/Scootly';

/** Konuma gidilirken kullanılan yakınlık: birkaç yüz metre çevresi görünür. */
export const FOCUS_ZOOM = 15;

/** Haritada dokunulunca sistem tarayıcısında açılabilecek bağlantılar (atıf metnindekiler). */
const EXTERNAL_LINK_HOSTS = [
  'openfreemap.org',
  'www.openmaptiles.org',
  'openmaptiles.org',
  'www.openstreetmap.org',
  'openstreetmap.org',
  'maplibre.org',
];

export function isAllowedExternalUrl(url: string): boolean {
  const match = /^https:\/\/([^/?#:]+)(?:[/?#]|$)/i.exec(url.trim());
  return match !== null && EXTERNAL_LINK_HOSTS.includes(match[1].toLowerCase());
}
