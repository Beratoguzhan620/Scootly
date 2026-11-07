import { isReservationExpired, reservationRemainingMs } from '../reservation';

const reservedAt = Date.UTC(2026, 9, 11, 8, 0, 0);
const minutes = (value: number) => value * 60_000;

describe('rezervasyon süresi (ReservationPolicy, 10 dakika)', () => {
  it('başta 10 dakika kalır', () => {
    expect(reservationRemainingMs(reservedAt, reservedAt)).toBe(minutes(10));
  });

  it('9 dakikada dolmamıştır', () => {
    expect(isReservationExpired(reservedAt, reservedAt + minutes(9))).toBe(false);
    expect(reservationRemainingMs(reservedAt, reservedAt + minutes(9))).toBe(minutes(1));
  });

  it('tam 10. dakikada dolar', () => {
    expect(isReservationExpired(reservedAt, reservedAt + minutes(10))).toBe(true);
    expect(reservationRemainingMs(reservedAt, reservedAt + minutes(10))).toBe(0);
  });

  it('kalan süre eksiye düşmez', () => {
    expect(reservationRemainingMs(reservedAt, reservedAt + minutes(30))).toBe(0);
  });
});
