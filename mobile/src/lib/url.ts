/**
 * Kullanıcının yazdığı API adresini düzenler: boşlukları atar, şema yoksa `http://` ekler, sondaki eğik
 * çizgileri kaldırır. Geçersizse null döner. (React Native'in `URL` desteği kısıtlı olduğu için düzenli ifade.)
 *
 *   "192.168.1.20:5017"         → "http://192.168.1.20:5017"
 *   "https://api.scootly.dev/"  → "https://api.scootly.dev"
 */
export function normalizeApiUrl(input: string): string | null {
  let value = input.trim();
  if (!value) return null;
  if (!/^[a-z][a-z0-9+.-]*:\/\//i.test(value)) value = `http://${value}`;
  value = value.replace(/\/+$/, '');

  const match = /^(https?):\/\/([a-z0-9.-]+|\[[0-9a-f:.]+\])(?::(\d{1,5}))?(\/[^\s?#]*)?$/i.exec(value);
  if (!match) return null;

  const [, scheme, host, port] = match;
  if (port !== undefined && (Number(port) < 1 || Number(port) > 65_535)) return null;
  if (host.startsWith('.') || host.endsWith('.') || host.includes('..')) return null;

  return `${scheme.toLowerCase()}://${value.slice(scheme.length + 3)}`;
}

/** Adres cihazın kendisini mi gösteriyor? (Telefonda `localhost` bilgisayara değil telefona gider.) */
export function isLoopbackUrl(url: string): boolean {
  return /^https?:\/\/(localhost|127(?:\.\d{1,3}){3}|\[::1\])(?::\d+)?(?:\/|$)/i.test(url);
}
