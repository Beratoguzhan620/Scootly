/** 4'ün katları; ekran kenar boşluğu 20. */
export const space = {
  xxs: 2,
  xs: 4,
  sm: 8,
  md: 12,
  lg: 16,
  xl: 20,
  xxl: 24,
  xxxl: 32,
} as const;

/** Köşe yarıçapı hiyerarşiyi gösterir: panel > yüzey > kontrol > plaka. */
export const radius = {
  plate: 8,
  control: 14,
  surface: 20,
  sheet: 28,
  pill: 999,
} as const;

/** En küçük dokunma alanı (WCAG 2.5.8 / platform yönergeleri). */
export const minTouchSize = 44;

/** Devre dışı öğelerin opaklığı */
export const disabledOpacity = 0.5;

/** Ekranın yatay kenar boşluğu */
export const screenPadding = space.xl;
