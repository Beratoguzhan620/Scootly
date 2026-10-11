import {
  formatCurrency,
  formatDateTime,
  formatDistance,
  formatDuration,
  formatMinutes,
  parseServerDate,
  shortId,
} from '../format';

describe('formatCurrency', () => {
  it.each([
    [0, '₺0,00'],
    [2.5, '₺2,50'],
    [47.5, '₺47,50'],
    [1234.5, '₺1.234,50'],
    [1234567.891, '₺1.234.567,89'],
    [-12, '-₺12,00'],
  ])('%d → %s', (amount, expected) => {
    expect(formatCurrency(amount)).toBe(expected);
  });
});

describe('formatDuration', () => {
  it.each([
    [0, '00:00'],
    [65_000, '01:05'],
    [9 * 60_000 + 42_000, '09:42'],
    [3_725_000, '1:02:05'],
    [-1_000, '00:00'],
  ])('%i ms → %s', (ms, expected) => {
    expect(formatDuration(ms)).toBe(expected);
  });
});

describe('formatMinutes', () => {
  it.each([
    [18 * 60_000, '18 dk'],
    [65 * 60_000, '1 sa 5 dk'],
    [120 * 60_000, '2 sa'],
  ])('%i ms → %s', (ms, expected) => {
    expect(formatMinutes(ms)).toBe(expected);
  });
});

describe('formatDistance', () => {
  it.each([
    [3, '10 m'],
    [349, '350 m'],
    [1200, '1,2 km'],
  ])('%d m → %s', (meters, expected) => {
    expect(formatDistance(meters)).toBe(expected);
  });
});

describe('tarih', () => {
  it('paneldeki gibi gg.aa.yyyy SS:dd biçimi (yerel saat)', () => {
    expect(formatDateTime(new Date(2026, 9, 11, 14, 5))).toBe('11.10.2026 14:05');
  });

  it('saat dilimi olmayan API tarihini UTC kabul eder', () => {
    expect(parseServerDate('2026-10-11T08:00:00').toISOString()).toBe('2026-10-11T08:00:00.000Z');
    expect(parseServerDate('2026-10-11T08:00:00.1234567Z').toISOString()).toBe('2026-10-11T08:00:00.123Z');
    expect(parseServerDate('2026-10-11T11:00:00+03:00').toISOString()).toBe('2026-10-11T08:00:00.000Z');
  });
});

it('kısa kimlik ilk 8 karakterdir', () => {
  expect(shortId('3f2b8c1a-0000-4000-8000-000000000000')).toBe('3f2b8c1a');
});
