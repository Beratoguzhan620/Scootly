import {
  formatCurrency,
  formatDateTime,
  formatDayMonth,
  formatDistance,
  formatDuration,
  formatMinutes,
  formatMonthYear,
  formatRelativeDateTime,
  formatWalk,
  monthKey,
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

describe('formatWalk', () => {
  it.each([
    [0, '1 dk'],
    [40, '1 dk'],
    [240, '3 dk'],
    [1500, '20 dk'],
  ])('%d m → %s', (meters, expected) => {
    expect(formatWalk(meters)).toBe(expected);
  });
});

describe('Türkçe tarih biçimleri', () => {
  it('ay adı ve kısa gün', () => {
    expect(formatMonthYear(new Date(2026, 9, 24))).toBe('Ekim 2026');
    expect(formatMonthYear(new Date(2026, 1, 1))).toBe('Şubat 2026');
    expect(formatDayMonth(new Date(2026, 7, 5))).toBe('5 Ağu');
  });

  it('bugün, dün ve daha eskisi', () => {
    const now = new Date(2026, 9, 24, 18, 0);
    expect(formatRelativeDateTime(new Date(2026, 9, 24, 14, 32), now)).toBe('Bugün 14:32');
    expect(formatRelativeDateTime(new Date(2026, 9, 23, 23, 59), now)).toBe('Dün 23:59');
    expect(formatRelativeDateTime(new Date(2026, 9, 2, 9, 5), now)).toBe('2 Eki 09:05');
  });

  it('aynı ayın tarihleri aynı anahtarı alır', () => {
    expect(monthKey(new Date(2026, 9, 1))).toBe(monthKey(new Date(2026, 9, 31, 23)));
    expect(monthKey(new Date(2026, 9, 1))).not.toBe(monthKey(new Date(2025, 9, 1)));
  });
});
