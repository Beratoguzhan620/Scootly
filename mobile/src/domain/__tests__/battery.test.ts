import { batteryLevel, batteryPercent } from '../battery';

describe('batarya', () => {
  it('kiralama sınırlarına göre durum', () => {
    expect(batteryLevel(86)).toBe('ok');
    expect(batteryLevel(20)).toBe('ok');
    expect(batteryLevel(19)).toBe('low');
    expect(batteryLevel(10)).toBe('low');
    expect(batteryLevel(9)).toBe('unrentable');
  });

  it('yüzdeyi 0–100 arasında tam sayıya çevirir', () => {
    expect(batteryPercent(54.6)).toBe(55);
    expect(batteryPercent(140)).toBe(100);
    expect(batteryPercent(-3)).toBe(0);
    expect(batteryPercent(Number.NaN)).toBe(0);
  });
});
