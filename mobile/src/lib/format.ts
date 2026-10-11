const pad = (value: number) => String(value).padStart(2, '0');

/** "₺1.234,50" — Intl'e bağımlı olmadan, her platformda aynı çıktı. */
export function formatCurrency(amount: number): string {
  const [integer, fraction] = Math.abs(amount).toFixed(2).split('.');
  const grouped = integer.replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  return `${amount < 0 ? '-' : ''}₺${grouped},${fraction}`;
}

/** Panelle aynı biçim: "11.10.2026 14:05" (yerel saat). */
export function formatDateTime(date: Date): string {
  return `${pad(date.getDate())}.${pad(date.getMonth() + 1)}.${date.getFullYear()} ${formatTime(date)}`;
}

export function formatTime(date: Date): string {
  return `${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

/** Sayaç biçimi: "09:42" ya da bir saati geçince "1:05:09". */
export function formatDuration(ms: number): string {
  const totalSeconds = Math.max(0, Math.floor(ms / 1000));
  const hours = Math.floor(totalSeconds / 3600);
  const minutes = Math.floor((totalSeconds % 3600) / 60);
  const seconds = totalSeconds % 60;
  return hours > 0 ? `${hours}:${pad(minutes)}:${pad(seconds)}` : `${pad(minutes)}:${pad(seconds)}`;
}

/** Liste özeti için: "18 dk", "1 sa 5 dk". */
export function formatMinutes(ms: number): string {
  const totalMinutes = Math.max(0, Math.round(ms / 60_000));
  if (totalMinutes < 60) return `${totalMinutes} dk`;
  const hours = Math.floor(totalMinutes / 60);
  const minutes = totalMinutes % 60;
  return minutes === 0 ? `${hours} sa` : `${hours} sa ${minutes} dk`;
}

/** "350 m", "1,2 km" */
export function formatDistance(meters: number): string {
  if (meters < 1000) return `${Math.max(10, Math.round(meters / 10) * 10)} m`;
  return `${(meters / 1000).toFixed(1).replace('.', ',')} km`;
}

/** Panel gibi kimliklerin ilk 8 karakteri. */
export function shortId(id: string): string {
  return id.slice(0, 8);
}

/**
 * API tarihleri UTC'dir. Saat dilimi bilgisi olmadan gelirse JavaScript onları yerel saat sanar;
 * bunu önlemek için sona "Z" eklenir.
 */
export function parseServerDate(value: string): Date {
  const hasZone = /(?:Z|[+-]\d{2}:?\d{2})$/i.test(value);
  return new Date(hasZone ? value : `${value}Z`);
}
