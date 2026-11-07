/**
 * JWT'nin yalnızca gövdesini okur (imza doğrulaması sunucunun işi). Uygulama bunu token'ın
 * ne zaman sona ereceğini bilmek için kullanır.
 */
export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.');
  if (parts.length !== 3 || !parts[1]) return null;

  try {
    const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
    const padded = base64 + '='.repeat((4 - (base64.length % 4)) % 4);
    const binary = atob(padded);
    // UTF-8 baytlarını doğru çözmek için (ör. e-postadaki Türkçe karakterler).
    const json = decodeURIComponent(
      Array.from(binary, (char) => `%${char.charCodeAt(0).toString(16).padStart(2, '0')}`).join(''),
    );
    const payload: unknown = JSON.parse(json);
    return typeof payload === 'object' && payload !== null ? (payload as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

/** Token'ın bitiş zamanı (epoch ms) ya da okunamıyorsa null. */
export function getJwtExpiry(token: string): number | null {
  const exp = decodeJwtPayload(token)?.exp;
  return typeof exp === 'number' ? exp * 1000 : null;
}

/** Saat farkı payıyla birlikte süresi dolmuş mu? Okunamayan token geçerli sayılır; karar sunucunundur. */
export function isJwtExpired(token: string, now: number = Date.now(), skewMs = 30_000): boolean {
  const expiry = getJwtExpiry(token);
  return expiry !== null && expiry - skewMs <= now;
}
