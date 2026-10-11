import { Platform } from 'react-native';

/**
 * Scootly tasarım dili — web panelinden (Scootly.Mvc) çıkarıldı.
 *
 * Panel, Bootstrap 5.3.3'ü varsayılan haliyle kullanıyor. Global `site.css` yalnızca odak halkasını
 * (#258cfb) değiştiriyor. `_Layout.cshtml.css` ise Razor CSS yalıtımıyla (scoped) yalnızca _Layout'un kendi
 * öğelerine uygulanıyor: footer'daki #e5e5e5 çizgi görünür, ama `.btn-primary { #1b6ec2 }` görünümlerdeki
 * butonlara ulaşmaz. Bu yüzden kullanıcının panelde gördüğü butonlar Bootstrap'in #0d6efd mavisidir;
 * buradaki değerler de onu esas alır.
 */

export const colors = {
  // Bootstrap tema renkleri
  primary: '#0d6efd',
  secondary: '#6c757d',
  success: '#198754',
  info: '#0dcaf0',
  warning: '#ffc107',
  danger: '#dc3545',
  light: '#f8f9fa',
  dark: '#212529',
  white: '#ffffff',
  black: '#000000',

  // Gövde
  body: '#212529',
  bodyBg: '#ffffff',
  /** `.text-muted` — Bootstrap 5.3'te `--bs-secondary-color`. */
  muted: 'rgba(33, 37, 41, 0.75)',
  border: '#dee2e6',
  borderTranslucent: 'rgba(0, 0, 0, 0.175)',
  secondaryBg: '#e9ecef',
  tertiaryBg: '#f8f9fa',
  /** `.table-striped` satır arka planı. */
  stripe: 'rgba(0, 0, 0, 0.05)',
  /** `.card-header` arka planı. */
  cardCap: 'rgba(33, 37, 41, 0.03)',
  /** `<code>` — panel kısa kimlikleri böyle gösteriyor. */
  code: '#d63384',
  link: '#0d6efd',

  // Panel özelleştirmeleri
  /** Odak halkası (site.css). */
  focusRing: '#258cfb',
  /** Form alanı odaklanınca kenarlık (Bootstrap `--bs-focus-ring` tonu). */
  focusBorder: '#86b7fe',
  /** Footer üst çizgisi (_Layout.cshtml.css). */
  divider: '#e5e5e5',

  // Navbar (`navbar-dark bg-dark`)
  navbar: '#212529',
  navbarText: '#ffffff',
  /** Pasif menü bağlantısı. */
  navbarLink: 'rgba(255, 255, 255, 0.55)',
  /** `.text-white-50` — panelde oturum açan kullanıcının adı. */
  navbarSubtle: 'rgba(255, 255, 255, 0.5)',
} as const;

export type Tone = 'primary' | 'secondary' | 'success' | 'info' | 'warning' | 'danger' | 'light' | 'dark';

/** Bootstrap 5.3 `*-bg-subtle`, `*-border-subtle`, `*-text-emphasis` üçlüleri (alert'ler bunları kullanır). */
export const subtle: Record<Exclude<Tone, 'light' | 'dark'>, { bg: string; border: string; text: string }> = {
  primary: { bg: '#cfe2ff', border: '#9ec5fe', text: '#052c65' },
  secondary: { bg: '#e2e3e5', border: '#c4c8cb', text: '#2b2f32' },
  success: { bg: '#d1e7dd', border: '#a3cfbb', text: '#0a3622' },
  info: { bg: '#cff4fc', border: '#9eeaf9', text: '#055160' },
  warning: { bg: '#fff3cd', border: '#ffe69c', text: '#664d03' },
  danger: { bg: '#f8d7da', border: '#f1aeb5', text: '#58151c' },
};

/** Bootstrap boşluk ölçeği: 1 = .25rem, 2 = .5rem, 3 = 1rem, 4 = 1.5rem, 5 = 3rem. */
export const spacing = {
  0: 0,
  1: 4,
  2: 8,
  3: 16,
  4: 24,
  5: 48,
} as const;

/** `.rounded-*` ölçeği; varsayılan .375rem. */
export const radius = {
  sm: 4,
  md: 6,
  lg: 8,
  pill: 999,
} as const;

const monospace = Platform.select({ ios: 'Menlo', android: 'monospace', default: 'monospace' });

/**
 * Bootstrap tipografisi: sistem yazı tipi, gövde 1.5 satır yüksekliği, başlıklar 500 ağırlık ve 1.2 satır
 * yüksekliği. Boyutlar 16px tabana ve ~390pt telefon genişliğine göre hesaplanmış duyarlı başlık boyutlarıdır.
 */
export const typography = {
  h1: { fontSize: 28, lineHeight: 34, fontWeight: '500' },
  h2: { fontSize: 25, lineHeight: 30, fontWeight: '500' },
  h3: { fontSize: 23, lineHeight: 28, fontWeight: '500' },
  h4: { fontSize: 21, lineHeight: 26, fontWeight: '500' },
  h5: { fontSize: 20, lineHeight: 24, fontWeight: '500' },
  h6: { fontSize: 16, lineHeight: 20, fontWeight: '500' },
  /** `.lead` */
  lead: { fontSize: 20, lineHeight: 30, fontWeight: '300' },
  body: { fontSize: 16, lineHeight: 24, fontWeight: '400' },
  /** `.small`, `.form-text`, `.invalid-feedback` (.875em) */
  small: { fontSize: 14, lineHeight: 21, fontWeight: '400' },
  /** `.badge` (.75em, kalın) */
  badge: { fontSize: 12, lineHeight: 14, fontWeight: '700' },
  /** `.navbar-brand` */
  brand: { fontSize: 20, lineHeight: 28, fontWeight: '400' },
  monospaceFamily: monospace,
} as const;

/**
 * Gölgeler (Bootstrap `.shadow-sm`, `.shadow`, `.shadow-lg`). React Native'in yeni mimarisi CSS
 * sözdizimindeki `boxShadow`'u destekliyor; değerler Bootstrap ile aynı.
 */
export const shadows = {
  sm: '0px 2px 4px rgba(0, 0, 0, 0.075)',
  md: '0px 8px 16px rgba(0, 0, 0, 0.15)',
  lg: '0px 16px 48px rgba(0, 0, 0, 0.175)',
  /** Panelin odak halkası: önce ince beyaz, sonra #258cfb. */
  focus: `0px 0px 0px 1.6px #ffffff, 0px 0px 0px 4px ${colors.focusRing}`,
} as const;

/** Butonlar devre dışıyken Bootstrap'in kullandığı opaklık. */
export const disabledOpacity = 0.65;

/** Dokunma hedefleri için platform önerisine uygun en küçük yükseklik. */
export const minTouchSize = 44;
