import {
  addRecord,
  MAX_HISTORY,
  parseHistory,
  serializeHistory,
  utf8Length,
  type ReservationRecord,
} from '../reservationHistory';

const record = (index: number, extra: Partial<ReservationRecord> = {}): ReservationRecord => ({
  vehicleId: `3f2b8c1a-1111-4000-8000-${String(index).padStart(12, '0')}`,
  brand: 'Segway Ninebot',
  reservedAt: 1_761_830_000_000 + index * 60_000,
  endedAt: 1_761_830_300_000 + index * 60_000,
  outcome: 'ride',
  rideId: `9a7e4d2b-2222-4000-8000-${String(index).padStart(12, '0')}`,
  card: 'Visa •••• 4242',
  ...extra,
});

describe('rezervasyon geçmişi', () => {
  it('kaydı bozmadan saklar ve geri okur', () => {
    const history = [record(1), record(2, { outcome: 'cancelled', rideId: undefined, card: undefined })];
    expect(parseHistory(serializeHistory(history))).toEqual(history);
  });

  it('şifreli depolamanın sınırını aşmaz; eski kayıtları düşürür', () => {
    const history = Array.from({ length: 20 }, (_, index) => record(index, { brand: 'Çok uzun bir marka adı '.repeat(3) }));
    const json = serializeHistory(history);
    expect(utf8Length(json)).toBeLessThanOrEqual(1_800);
    const parsed = parseHistory(json);
    expect(parsed.length).toBeGreaterThan(0);
    expect(parsed.length).toBeLessThanOrEqual(MAX_HISTORY);
    expect(parsed[0].vehicleId).toBe(history[0].vehicleId);
  });

  it('yeni kaydı başa ekler, aynı rezervasyonu ikiye katlamaz', () => {
    const first = record(1, { outcome: 'expired', rideId: undefined });
    const updated = { ...first, outcome: 'ride' as const, rideId: 'r1' };
    const history = addRecord(addRecord([], first), record(2));
    expect(addRecord(history, updated).map((item) => item.outcome)).toEqual(['ride', 'ride']);
    expect(addRecord(history, updated)).toHaveLength(2);
  });

  it('bozuk kayıtları atlar', () => {
    expect(parseHistory(null)).toEqual([]);
    expect(parseHistory('{')).toEqual([]);
    expect(parseHistory('{"v":1}')).toEqual([]);
    expect(parseHistory('[{"v":"a","r":1,"e":2,"o":"bilinmiyor"},{"v":"b","r":1,"e":2,"o":"expired"}]')).toEqual([
      { vehicleId: 'b', reservedAt: 1, endedAt: 2, outcome: 'expired' },
    ]);
  });

  it('UTF-8 bayt sayısını doğru hesaplar', () => {
    expect(utf8Length('abc')).toBe(3);
    expect(utf8Length('ş')).toBe(2);
    expect(utf8Length('•')).toBe(3);
    expect(utf8Length('😀')).toBe(4);
  });
});
