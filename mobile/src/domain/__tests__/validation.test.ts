import { checkPassword, validateEmail, validatePassword } from '../validation';

const unmet = (password: string) =>
  checkPassword(password)
    .filter((rule) => !rule.met)
    .map((rule) => rule.key);

describe('parola kuralları (Identity ile aynı)', () => {
  it('8+ karakter, büyük/küçük harf ve rakam içeren parolayı kabul eder', () => {
    expect(unmet('Scootly2026')).toEqual([]);
    expect(validatePassword('Scootly2026')).toBeNull();
  });

  it('eksik kuralları listeler', () => {
    expect(unmet('kisa1A')).toEqual(['length']);
    expect(unmet('scootly2026')).toEqual(['upper']);
    expect(unmet('SCOOTLY2026')).toEqual(['lower']);
    expect(unmet('Scootlyyy')).toEqual(['digit']);
  });

  it('Türkçe büyük harfi büyük harf saymaz (Identity yalnızca A-Z sayar)', () => {
    expect(unmet('Şifre1234')).toEqual(['upper']);
    expect(validatePassword('Şifre1234')).not.toBeNull();
  });

  it('boş ve çok uzun parolayı reddeder', () => {
    expect(validatePassword('')).toBe('Parola zorunludur.');
    expect(validatePassword(`Aa1${'x'.repeat(130)}`)).toMatch(/en fazla 128/);
  });
});

describe('e-posta doğrulaması', () => {
  it('baştaki ve sondaki boşlukları yok sayar', () => {
    expect(validateEmail('  emre@example.com ')).toBeNull();
  });

  it('boş ve geçersiz adresleri reddeder', () => {
    expect(validateEmail('')).toBe('E-posta adresi zorunludur.');
    expect(validateEmail('emre')).toBe('Geçerli bir e-posta adresi girin.');
    expect(validateEmail('emre @example.com')).toBe('Geçerli bir e-posta adresi girin.');
  });
});
