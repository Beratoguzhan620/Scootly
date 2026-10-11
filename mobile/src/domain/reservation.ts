import { RESERVATION_DURATION_MS } from './rules';

/** Rezervasyonun bitmesine kalan süre (ms). Süre dolduysa 0. */
export function reservationRemainingMs(reservedAt: number, now: number): number {
  return Math.max(0, reservedAt + RESERVATION_DURATION_MS - now);
}

/** `ReservationPolicy.IsExpired` ile aynı: süre tam dolduğu anda biter. */
export function isReservationExpired(reservedAt: number, now: number): boolean {
  return now >= reservedAt + RESERVATION_DURATION_MS;
}
