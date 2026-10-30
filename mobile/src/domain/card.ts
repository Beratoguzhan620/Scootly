/**
 * Ödeme kartı (test modu). Backend kart bilgisi almıyor: sürüş bitince ücret sunucunun ödeme simülatöründen
 * tahsil edilir. Kart bu yüzden yalnızca telefonda tutulur ve tam numara ya da CVC asla saklanmaz; kayıtta
 * markası, son 4 hanesi, son kullanma tarihi ve kart sahibinin adı kalır.
 */

export type CardBrand = 'visa' | 'mastercard' | 'amex' | 'troy' | 'unknown';

export type SavedCard = {
  brand: CardBrand;
  last4: string;
  expMonth: number;
  /** Dört haneli yıl */
  expYear: number;
  holder: string;
};

export type CardInput = {
  number: string;
  /** "AA/YY" */
  expiry: string;
  cvc: string;
  holder: string;
};

export type CardField = keyof CardInput;

export const cardBrandLabels: Record<CardBrand, string> = {
  visa: 'Visa',
  mastercard: 'Mastercard',
  amex: 'American Express',
  troy: 'Troy',
  unknown: 'Kart',
};

/** "Visa •••• 4242" */
export function cardLabel(card: Pick<SavedCard, 'brand' | 'last4'>): string {
  return `${cardBrandLabels[card.brand]} •••• ${card.last4}`;
}

export function digitsOnly(value: string): string {
  return value.replace(/\D/g, '');
}

/** Kartın markası numaranın ilk hanelerinden anlaşılır (yazarken de çalışır). */
export function detectBrand(input: string): CardBrand {
  const digits = digitsOnly(input);
  if (digits.startsWith('4')) return 'visa';
  if (digits.startsWith('9792')) return 'troy';
  const first2 = Number(digits.slice(0, 2));
  if (digits.length >= 2 && (first2 === 34 || first2 === 37)) return 'amex';
  if (digits.length >= 2 && first2 >= 51 && first2 <= 55) return 'mastercard';
  const first4 = Number(digits.slice(0, 4));
  if (digits.length >= 4 && first4 >= 2221 && first4 <= 2720) return 'mastercard';
  return 'unknown';
}

const LENGTHS: Record<CardBrand, number[]> = {
  visa: [13, 16, 19],
  mastercard: [16],
  amex: [15],
  troy: [16],
  unknown: [12, 13, 14, 15, 16, 17, 18, 19],
};

export function cvcLength(brand: CardBrand): number {
  return brand === 'amex' ? 4 : 3;
}

/** Luhn (mod 10) sağlaması: yanlış yazılmış numaraların çoğunu yakalar. */
export function luhnValid(input: string): boolean {
  const digits = digitsOnly(input);
  if (digits.length < 12) return false;
  let sum = 0;
  for (let index = 0; index < digits.length; index += 1) {
    let digit = Number(digits[digits.length - 1 - index]);
    if (index % 2 === 1) {
      digit *= 2;
      if (digit > 9) digit -= 9;
    }
    sum += digit;
  }
  return sum % 10 === 0;
}

/** Yazarken dörtlü gruplar ("4242 4242 …"); American Express 4-6-5. */
export function formatCardNumber(input: string): string {
  const digits = digitsOnly(input).slice(0, 19);
  if (detectBrand(digits) === 'amex') {
    return [digits.slice(0, 4), digits.slice(4, 10), digits.slice(10, 15)].filter(Boolean).join(' ');
  }
  return digits.replace(/(\d{4})(?=\d)/g, '$1 ');
}

/** Yazarken "AA/YY". */
export function formatExpiry(input: string): string {
  const digits = digitsOnly(input).slice(0, 4);
  return digits.length > 2 ? `${digits.slice(0, 2)}/${digits.slice(2)}` : digits;
}

/** "08/29" → { month: 8, year: 2029 }; biçim bozuksa null. */
export function parseExpiry(input: string): { month: number; year: number } | null {
  const match = /^(\d{2})\s*\/\s*(\d{2})$/.exec(input.trim());
  if (!match) return null;
  const month = Number(match[1]);
  if (month < 1 || month > 12) return null;
  return { month, year: 2000 + Number(match[2]) };
}

/** Kart, son kullanma ayının sonuna kadar geçerlidir. */
export function isCardExpired(month: number, year: number, now: Date): boolean {
  return year < now.getFullYear() || (year === now.getFullYear() && month < now.getMonth() + 1);
}

export type CardValidation =
  | { ok: true; card: SavedCard }
  | { ok: false; errors: Partial<Record<CardField, string>> };

/** Formu denetler; geçerliyse saklanacak özet kaydı (son 4 hane) döner. */
export function validateCard(input: CardInput, now: Date = new Date()): CardValidation {
  const errors: Partial<Record<CardField, string>> = {};
  const digits = digitsOnly(input.number);
  const brand = detectBrand(digits);

  if (!digits) errors.number = 'Kart numarasını yaz.';
  else if (!LENGTHS[brand].includes(digits.length) || !luhnValid(digits)) errors.number = 'Kart numarası geçerli değil.';

  const expiry = parseExpiry(input.expiry);
  if (!input.expiry.trim()) errors.expiry = 'Son kullanma tarihini yaz.';
  else if (!expiry) errors.expiry = 'AA/YY biçiminde yaz.';
  else if (isCardExpired(expiry.month, expiry.year, now)) errors.expiry = 'Kartın süresi dolmuş.';
  else if (expiry.year > now.getFullYear() + 20) errors.expiry = 'Son kullanma tarihi geçerli değil.';

  const cvc = digitsOnly(input.cvc);
  if (!cvc) errors.cvc = 'Güvenlik kodunu yaz.';
  else if (cvc.length !== cvcLength(brand) || cvc !== input.cvc.trim()) {
    errors.cvc = `${cvcLength(brand)} haneli olmalı.`;
  }

  const holder = input.holder.trim().replace(/\s+/g, ' ');
  if (holder.length < 2) errors.holder = 'Kartın üstündeki adı yaz.';
  // Latin harfleri (Türkçe karakterler dahil); Hermes'te Unicode özellik kaçışlarına güvenilmez.
  else if (!/^[A-Za-zÀ-ÖØ-öø-ɏ .'-]+$/.test(holder)) errors.holder = 'Yalnızca harf kullan.';

  if (Object.keys(errors).length > 0 || !expiry) return { ok: false, errors };

  return {
    ok: true,
    card: { brand, last4: digits.slice(-4), expMonth: expiry.month, expYear: expiry.year, holder },
  };
}

/** Test modu için örnek kart (Visa test numarası; gerçek bir karta ait değil). */
export function testCardInput(now: Date = new Date()): CardInput {
  return {
    number: '4242 4242 4242 4242',
    expiry: `12/${String((now.getFullYear() + 3) % 100).padStart(2, '0')}`,
    cvc: '123',
    holder: 'Scootly Test',
  };
}

/** Kayıtlı özetin bozulmamış olduğunu denetler (cihazdan okunurken). */
export function parseSavedCard(value: string | null): SavedCard | null {
  if (!value) return null;
  try {
    const card = JSON.parse(value) as Partial<SavedCard>;
    if (
      typeof card.brand === 'string' &&
      card.brand in cardBrandLabels &&
      typeof card.last4 === 'string' &&
      /^\d{4}$/.test(card.last4) &&
      typeof card.expMonth === 'number' &&
      typeof card.expYear === 'number' &&
      typeof card.holder === 'string'
    ) {
      return {
        brand: card.brand as CardBrand,
        last4: card.last4,
        expMonth: card.expMonth,
        expYear: card.expYear,
        holder: card.holder,
      };
    }
  } catch {
    // bozuk kayıt
  }
  return null;
}

/** "08/29" */
export function formatCardExpiry(card: Pick<SavedCard, 'expMonth' | 'expYear'>): string {
  return `${String(card.expMonth).padStart(2, '0')}/${String(card.expYear % 100).padStart(2, '0')}`;
}
