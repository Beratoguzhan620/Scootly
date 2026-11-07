import {
  cardLabel,
  detectBrand,
  formatCardExpiry,
  formatCardNumber,
  formatExpiry,
  isCardExpired,
  luhnValid,
  parseExpiry,
  parseSavedCard,
  testCardInput,
  validateCard,
} from '../card';

const NOW = new Date(2026, 9, 30); // 30 Ekim 2026

describe('kart markası', () => {
  it('ilk hanelerden anlaşılır', () => {
    expect(detectBrand('4242')).toBe('visa');
    expect(detectBrand('5555 5555')).toBe('mastercard');
    expect(detectBrand('2223 0000')).toBe('mastercard');
    expect(detectBrand('3714')).toBe('amex');
    expect(detectBrand('9792 0300')).toBe('troy');
    expect(detectBrand('6011')).toBe('unknown');
    expect(detectBrand('')).toBe('unknown');
  });

  it('etiket yalnızca son 4 haneyi gösterir', () => {
    expect(cardLabel({ brand: 'visa', last4: '4242' })).toBe('Visa •••• 4242');
  });
});

describe('Luhn', () => {
  it('bilinen test numaralarını kabul eder, tek hane hatasını yakalar', () => {
    expect(luhnValid('4242 4242 4242 4242')).toBe(true);
    expect(luhnValid('5555555555554444')).toBe(true);
    expect(luhnValid('378282246310005')).toBe(true);
    expect(luhnValid('4242 4242 4242 4241')).toBe(false);
    expect(luhnValid('4242')).toBe(false);
  });
});

describe('biçimleme', () => {
  it('kart numarasını dörtlü gruplar, Amex için 4-6-5', () => {
    expect(formatCardNumber('4242424242424242')).toBe('4242 4242 4242 4242');
    expect(formatCardNumber('42424')).toBe('4242 4');
    expect(formatCardNumber('378282246310005')).toBe('3782 822463 10005');
    expect(formatCardNumber('4242-4242 abc')).toBe('4242 4242');
  });

  it('son kullanma tarihini AA/YY yazar', () => {
    expect(formatExpiry('1')).toBe('1');
    expect(formatExpiry('12')).toBe('12');
    expect(formatExpiry('123')).toBe('12/3');
    expect(formatExpiry('12/29x')).toBe('12/29');
    expect(formatCardExpiry({ expMonth: 8, expYear: 2029 })).toBe('08/29');
  });

  it('son kullanma tarihini çözer', () => {
    expect(parseExpiry('08/29')).toEqual({ month: 8, year: 2029 });
    expect(parseExpiry('13/29')).toBeNull();
    expect(parseExpiry('8/29')).toBeNull();
  });

  it('kart son kullanma ayının sonuna kadar geçerlidir', () => {
    expect(isCardExpired(10, 2026, NOW)).toBe(false);
    expect(isCardExpired(9, 2026, NOW)).toBe(true);
    expect(isCardExpired(1, 2027, NOW)).toBe(false);
  });
});

describe('validateCard', () => {
  it('geçerli kartın yalnızca özetini döner; tam numara ve CVC kayda girmez', () => {
    const result = validateCard({ number: '4242 4242 4242 4242', expiry: '08/29', cvc: '123', holder: ' Emre  Can ' }, NOW);
    expect(result).toEqual({
      ok: true,
      card: { brand: 'visa', last4: '4242', expMonth: 8, expYear: 2029, holder: 'Emre Can' },
    });
    expect(JSON.stringify(result)).not.toContain('4242 4242');
    expect(JSON.stringify(result)).not.toContain('123');
  });

  it('her alan için ne yapılacağını söyler', () => {
    const result = validateCard({ number: '4242 4242 4242 4241', expiry: '09/26', cvc: '12', holder: '1' }, NOW);
    expect(result.ok).toBe(false);
    if (result.ok) return;
    expect(result.errors.number).toBe('Kart numarası geçerli değil.');
    expect(result.errors.expiry).toBe('Kartın süresi dolmuş.');
    expect(result.errors.cvc).toBe('3 haneli olmalı.');
    expect(result.errors.holder).toBe('Kartın üstündeki adı yaz.');
  });

  it('Amex için 4 haneli güvenlik kodu ister, Türkçe adları kabul eder', () => {
    const amex = validateCard({ number: '378282246310005', expiry: '01/30', cvc: '123', holder: 'Şükrü Öğün' }, NOW);
    expect(amex.ok).toBe(false);
    if (!amex.ok) expect(amex.errors).toEqual({ cvc: '4 haneli olmalı.' });
    expect(validateCard({ number: '378282246310005', expiry: '01/30', cvc: '1234', holder: 'Şükrü Öğün' }, NOW).ok).toBe(
      true,
    );
  });

  it('boş formda alanları ister', () => {
    const result = validateCard({ number: '', expiry: '', cvc: '', holder: '' }, NOW);
    expect(result).toEqual({
      ok: false,
      errors: {
        number: 'Kart numarasını yaz.',
        expiry: 'Son kullanma tarihini yaz.',
        cvc: 'Güvenlik kodunu yaz.',
        holder: 'Kartın üstündeki adı yaz.',
      },
    });
  });

  it('test kartı her zaman geçerlidir', () => {
    expect(validateCard(testCardInput(NOW), NOW).ok).toBe(true);
  });
});

describe('parseSavedCard', () => {
  it('bozuk ya da eksik kaydı yok sayar', () => {
    expect(parseSavedCard(null)).toBeNull();
    expect(parseSavedCard('{')).toBeNull();
    expect(parseSavedCard(JSON.stringify({ brand: 'visa', last4: '42', expMonth: 1, expYear: 2030, holder: 'A' }))).toBeNull();
    expect(
      parseSavedCard(JSON.stringify({ brand: 'visa', last4: '4242', expMonth: 1, expYear: 2030, holder: 'A', extra: 1 })),
    ).toEqual({ brand: 'visa', last4: '4242', expMonth: 1, expYear: 2030, holder: 'A' });
  });
});
