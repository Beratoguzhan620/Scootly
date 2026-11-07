import { billableMinutes, estimateFare, roundMoney } from '../fare';

// Vakalar backend'deki `TariffAndPolicyTests` ile aynı: istemci tahmini sunucu hesabıyla birebir örtüşmeli.
describe('estimateFare (Tariff.Standard)', () => {
  it.each([
    [0, 2.5], // 0 sn → en az 1 dakika
    [30, 2.5], // 30 sn → 1 dakika
    [60, 2.5], // tam 1 dakika
    [61, 5.0], // başlamış 2. dakika
    [119, 5.0], // 1 dk 59 sn → 2 dakika
    [600, 25.0], // 10 dakika
  ])('%i saniye → %d TL', (seconds, expected) => {
    expect(estimateFare(seconds * 1000)).toBe(expected);
  });

  it('açılış ücretini ekler', () => {
    expect(estimateFare(3 * 60_000, { unlockFee: 5, perMinuteRate: 2 })).toBe(11);
  });

  it('kuruşları iki haneye yuvarlar', () => {
    expect(estimateFare(60_000, { unlockFee: 0.005, perMinuteRate: 1 })).toBe(1.01);
  });
});

describe('roundMoney (MidpointRounding.AwayFromZero)', () => {
  it.each([
    [1.005, 1.01],
    [2.675, 2.68],
    [0.1 + 0.2, 0.3],
    [-1.005, -1.01],
    [47.5, 47.5],
  ])('%d → %d', (value, expected) => {
    expect(roundMoney(value)).toBe(expected);
  });
});

describe('billableMinutes', () => {
  it('negatif süreyi 1 dakika sayar (istemci saati geride olabilir)', () => {
    expect(billableMinutes(-5_000)).toBe(1);
  });
});
