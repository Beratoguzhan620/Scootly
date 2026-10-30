/**
 * Telefondaki son rezervasyonlar. Backend'de rezervasyon ayrı bir kayıt değil, aracın o anki durumu olduğu için
 * geçmişi yoktur; "Rezervasyonlar" sayfasındaki geçmiş bu telefonda tutulur. Şifreli depolamanın küçük kayıt
 * sınırına (~2 KB) sığsın diye kısa alan adlarıyla ve en fazla birkaç kayıtla saklanır.
 */

/** ride: sürüşe dönüştü · cancelled: iptal edildi · expired: süresi doldu · released: başka yerde sonlandı */
export type ReservationOutcome = 'ride' | 'cancelled' | 'expired' | 'released';

export type ReservationRecord = {
  vehicleId: string;
  brand?: string;
  /** epoch ms */
  reservedAt: number;
  /** epoch ms */
  endedAt: number;
  outcome: ReservationOutcome;
  /** Sürüşe dönüştüyse */
  rideId?: string;
  /** Sürüşe dönüştüyse ödeme için seçilen kart: "Visa •••• 4242" */
  card?: string;
};

export const MAX_HISTORY = 8;
const MAX_BYTES = 1_800;
const OUTCOMES: ReservationOutcome[] = ['ride', 'cancelled', 'expired', 'released'];

type Compact = { v: string; b?: string; r: number; e: number; o: ReservationOutcome; i?: string; c?: string };

/** UTF-8 bayt sayısı (depolama sınırı bayt cinsinden). */
export function utf8Length(value: string): number {
  let bytes = 0;
  for (const char of value) {
    const code = char.codePointAt(0) ?? 0;
    bytes += code < 0x80 ? 1 : code < 0x800 ? 2 : code < 0x10000 ? 3 : 4;
  }
  return bytes;
}

function compact(record: ReservationRecord): Compact {
  return {
    v: record.vehicleId,
    ...(record.brand ? { b: record.brand.slice(0, 40) } : {}),
    r: record.reservedAt,
    e: record.endedAt,
    o: record.outcome,
    ...(record.rideId ? { i: record.rideId } : {}),
    ...(record.card ? { c: record.card } : {}),
  };
}

/** En yeniden eskiye; sığmayan eski kayıtlar düşer. */
export function serializeHistory(records: ReservationRecord[]): string {
  let kept = records.slice(0, MAX_HISTORY).map(compact);
  let json = JSON.stringify(kept);
  while (kept.length > 0 && utf8Length(json) > MAX_BYTES) {
    kept = kept.slice(0, -1);
    json = JSON.stringify(kept);
  }
  return json;
}

export function parseHistory(value: string | null): ReservationRecord[] {
  if (!value) return [];
  try {
    const parsed: unknown = JSON.parse(value);
    if (!Array.isArray(parsed)) return [];
    return parsed.flatMap((item: Partial<Compact>) =>
      item &&
      typeof item.v === 'string' &&
      typeof item.r === 'number' &&
      typeof item.e === 'number' &&
      OUTCOMES.includes(item.o as ReservationOutcome)
        ? [
            {
              vehicleId: item.v,
              brand: typeof item.b === 'string' ? item.b : undefined,
              reservedAt: item.r,
              endedAt: item.e,
              outcome: item.o as ReservationOutcome,
              rideId: typeof item.i === 'string' ? item.i : undefined,
              card: typeof item.c === 'string' ? item.c : undefined,
            },
          ]
        : [],
    );
  } catch {
    return [];
  }
}

/** Yeni kaydı başa ekler; aynı rezervasyonun (araç + başlangıç) eski kaydını değiştirir. */
export function addRecord(history: ReservationRecord[], record: ReservationRecord): ReservationRecord[] {
  const others = history.filter(
    (item) => !(item.vehicleId === record.vehicleId && item.reservedAt === record.reservedAt),
  );
  return [record, ...others].slice(0, MAX_HISTORY);
}
