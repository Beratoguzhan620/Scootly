import { EMAIL_MAX_LENGTH, PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH } from './rules';

export type PasswordRule = {
  key: 'length' | 'upper' | 'lower' | 'digit';
  label: string;
  met: boolean;
};

/**
 * ASP.NET Identity'nin parola doğrulayıcısıyla aynı kurallar. Identity büyük/küçük harf ve rakamı
 * yalnızca ASCII aralığında sayar (A-Z, a-z, 0-9); bu yüzden "Ş" büyük harf yerine geçmez.
 * Sunucunun İngilizce hata mesajlarını kullanıcıya göstermemek için kurallar istemcide de denetlenir.
 */
export function checkPassword(password: string): PasswordRule[] {
  return [
    { key: 'length', label: `En az ${PASSWORD_MIN_LENGTH} karakter`, met: password.length >= PASSWORD_MIN_LENGTH },
    { key: 'upper', label: 'En az bir büyük harf (A-Z)', met: /[A-Z]/.test(password) },
    { key: 'lower', label: 'En az bir küçük harf (a-z)', met: /[a-z]/.test(password) },
    { key: 'digit', label: 'En az bir rakam (0-9)', met: /[0-9]/.test(password) },
  ];
}

export function validatePassword(password: string): string | null {
  if (!password) return 'Parola zorunludur.';
  if (password.length > PASSWORD_MAX_LENGTH) return `Parola en fazla ${PASSWORD_MAX_LENGTH} karakter olabilir.`;
  const unmet = checkPassword(password).filter((rule) => !rule.met);
  return unmet.length === 0 ? null : 'Parola kurallarının tümünü karşılamalı.';
}

/** `CredentialsValidator` gibi gevşek bir kontrol: sunucu `MailAddress.TryCreate` kullanıyor. */
export function validateEmail(email: string): string | null {
  const value = email.trim();
  if (!value) return 'E-posta adresi zorunludur.';
  if (value.length > EMAIL_MAX_LENGTH || !/^[^\s@]+@[^\s@]+$/.test(value)) return 'Geçerli bir e-posta adresi girin.';
  return null;
}
