import { STANDARD_TARIFF } from './rules';

type Tariff = { unlockFee: number; perMinuteRate: number };

/** Ücretlendirilen dakika: başlamış her dakika sayılır, en az 1. (`Tariff.Calculate`) */
export function billableMinutes(durationMs: number): number {
  if (durationMs < 0) return 1;
  return Math.max(1, Math.ceil(durationMs / 60_000));
}

/**
 * C# `Math.Round(value, 2, MidpointRounding.AwayFromZero)` karşılığı. Sunucu `decimal` ile hesaplıyor;
 * JavaScript'te 1.005 ikili sayı olarak 1.00499… olduğu için düz `Math.round(x * 100)` 1 kuruş sapar.
 * Önce kayan nokta gürültüsü temizlenir, sonra ondalık kaydırma metin üzerinden yapılır.
 */
export function roundMoney(value: number, decimals = 2): number {
  const cleaned = Number(value.toFixed(10));
  const shifted = Number(`${Math.abs(cleaned)}e${decimals}`);
  return Math.sign(cleaned) * Number(`${Math.round(shifted)}e-${decimals}`);
}

/**
 * Sunucudaki `Tariff.Calculate` ile aynı hesap. Sürüş sırasında gösterilen "tahmini ücret" içindir;
 * kesin ücret sürüş bitince API'den gelir.
 */
export function estimateFare(durationMs: number, tariff: Tariff = STANDARD_TARIFF): number {
  return roundMoney(tariff.unlockFee + billableMinutes(durationMs) * tariff.perMinuteRate);
}
