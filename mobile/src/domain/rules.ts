/**
 * Backend iş kurallarının istemci tarafındaki kopyası. Sunucu her kuralı kendisi de uygular; buradakiler
 * yalnızca kullanıcıyı istek atmadan önce bilgilendirmek ve tahmini değerleri göstermek içindir.
 * Backend'de değişirse burası da güncellenmeli (kaynaklar yorumlarda).
 */

/** `ReservationPolicy.Duration` */
export const RESERVATION_DURATION_MS = 10 * 60 * 1000;

/** `BatteryLevel.MinimumRentablePercentage` — altındaki araç rezerve edilemez. */
export const MIN_RENTABLE_BATTERY = 10;

/** `BatteryLevel.LowThresholdPercentage` — altında saha görevi açılır; arayüzde uyarı rengi. */
export const LOW_BATTERY_THRESHOLD = 20;

/** `Tariff.Standard` — açılış bedeli + başlamış her dakika için dakika ücreti (en az 1 dakika). */
export const STANDARD_TARIFF = {
  unlockFee: 0,
  perMinuteRate: 2.5,
} as const;

/** Identity parola kuralları (`InfrastructureDependencyInjection`) ve `CredentialsValidator` sınırları. */
export const PASSWORD_MIN_LENGTH = 8;
export const PASSWORD_MAX_LENGTH = 128;
export const EMAIL_MAX_LENGTH = 256;

/** Hesap kilitleme: 5 hatalı denemede 15 dakika. */
export const LOCKOUT_MAX_ATTEMPTS = 5;
export const LOCKOUT_MINUTES = 15;

/** `Paging.MaxPageSize` */
export const MAX_PAGE_SIZE = 100;
