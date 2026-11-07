/**
 * Scootly renkleri — "sokak tabelası" dili: voltaj sarısı levha, derin lacivert yazı.
 *
 * Sarı yalnızca dolgu olarak kullanılır (üstünde her zaman lacivert yazı); beyaz zeminde metin rengi olmaz.
 * Sarı, ekranda "burada işlem yap" anlamına gelir: seçili scooter, ana buton, etkin sekme.
 */

/** Marka sarısı (dolgu). Üstündeki yazı her zaman `INK`. */
export const VOLT = '#FFD60A';
/** Derin lacivert: başlıklar, sarı üstündeki yazı, harita işaretlerinin çerçevesi. */
export const INK = '#0B1E4A';

export type Scheme = 'light' | 'dark';

/** Durum renkleri: `fg` metin ve simge, `bg` yumuşak zemin, `solid` dolu nokta/çizgi. */
export type ToneColors = { fg: string; bg: string; solid: string };

export type Theme = {
  scheme: Scheme;
  /** Ekran zemini */
  bg: string;
  /** Kartlar, alt panel, sekme çubuğu */
  surface: string;
  /** Form alanları, basılı satırlar, ikincil butonlar */
  surfaceAlt: string;
  text: string;
  textMuted: string;
  /** Yer tutucu ve devre dışı metin */
  textSubtle: string;
  line: string;
  lineStrong: string;
  brand: string;
  brandPressed: string;
  onBrand: string;
  /** Güçlü ikincil buton: açık temada lacivert dolgu, koyu temada açık dolgu. */
  strong: string;
  strongPressed: string;
  onStrong: string;
  /** Odaklanan form alanının çerçevesi */
  focus: string;
  positive: ToneColors;
  attention: ToneColors;
  critical: ToneColors;
  info: ToneColors;
  neutral: ToneColors;
  /** Marka sarısının yumuşak tonu (etkin sürüş rozeti, bilgi şeridi) */
  brandSoft: ToneColors;
  /** Sürüş sırasında alt panel ters renklerde: lacivert zemin, sarı rakamlar. */
  ride: { bg: string; text: string; muted: string; accent: string; line: string };
  /** Yüzen öğelerin gölgesi (boxShadow) */
  floatShadow: string;
  /** Alt panelin üst gölgesi */
  sheetShadow: string;
  /** Kullanıcının konum noktası */
  userDot: string;
  statusBar: 'light' | 'dark';
};

export const light: Theme = {
  scheme: 'light',
  bg: '#F3F5F9',
  surface: '#FFFFFF',
  surfaceAlt: '#EBEFF5',
  text: INK,
  textMuted: '#55627F',
  textSubtle: '#8A95AD',
  line: '#E1E6EF',
  lineStrong: '#C9D1DE',
  brand: VOLT,
  brandPressed: '#F2C700',
  onBrand: INK,
  strong: INK,
  strongPressed: '#1A3170',
  onStrong: '#FFFFFF',
  focus: INK,
  positive: { fg: '#11805A', bg: '#E3F4EC', solid: '#16A06E' },
  attention: { fg: '#8A5300', bg: '#FFF4CC', solid: '#F5A300' },
  critical: { fg: '#C2271B', bg: '#FDECEA', solid: '#D92D20' },
  info: { fg: '#2347A8', bg: '#E7EEFC', solid: '#2D5BFF' },
  neutral: { fg: '#55627F', bg: '#EBEFF5', solid: '#8A95AD' },
  brandSoft: { fg: INK, bg: '#FFF3B3', solid: VOLT },
  ride: { bg: INK, text: '#FFFFFF', muted: '#AEB9D3', accent: VOLT, line: 'rgba(255,255,255,0.14)' },
  floatShadow: '0px 4px 14px rgba(11, 30, 74, 0.18)',
  sheetShadow: '0px -6px 24px rgba(11, 30, 74, 0.12)',
  userDot: '#2D5BFF',
  statusBar: 'dark',
};

export const dark: Theme = {
  scheme: 'dark',
  bg: '#0A1428',
  surface: '#13213D',
  surfaceAlt: '#1B2B4D',
  text: '#EEF2FA',
  textMuted: '#9AA7C2',
  textSubtle: '#6E7C9C',
  line: '#22335A',
  lineStrong: '#2F4372',
  brand: VOLT,
  brandPressed: '#E8C300',
  onBrand: INK,
  strong: '#EEF2FA',
  strongPressed: '#C9D3E6',
  onStrong: INK,
  focus: VOLT,
  positive: { fg: '#45D19A', bg: 'rgba(69, 209, 154, 0.14)', solid: '#2DBE84' },
  attention: { fg: '#FFC93D', bg: 'rgba(255, 201, 61, 0.14)', solid: '#F5A300' },
  critical: { fg: '#FF7A6E', bg: 'rgba(255, 122, 110, 0.14)', solid: '#FF5A4E' },
  info: { fg: '#8FB0FF', bg: 'rgba(143, 176, 255, 0.14)', solid: '#5B8CFF' },
  neutral: { fg: '#9AA7C2', bg: '#1B2B4D', solid: '#6E7C9C' },
  brandSoft: { fg: VOLT, bg: 'rgba(255, 214, 10, 0.14)', solid: VOLT },
  ride: { bg: '#102A66', text: '#FFFFFF', muted: '#AEB9D3', accent: VOLT, line: 'rgba(255,255,255,0.14)' },
  floatShadow: '0px 4px 14px rgba(0, 0, 0, 0.45)',
  sheetShadow: '0px -6px 24px rgba(0, 0, 0, 0.35)',
  userDot: '#5B8CFF',
  statusBar: 'light',
};

export const themes: Record<Scheme, Theme> = { light, dark };

/**
 * Durum rozetlerinin anlamsal tonları. `domain/labels.ts` araç, sürüş ve ödeme durumlarını bunlara eşler.
 * (Adlar web panelindeki rozet sınıflarından geliyor.)
 */
export type Tone = 'primary' | 'secondary' | 'success' | 'info' | 'warning' | 'danger' | 'light' | 'dark';

export function toneColors(theme: Theme, tone: Tone): ToneColors {
  switch (tone) {
    case 'primary':
      return theme.brandSoft;
    case 'success':
      return theme.positive;
    case 'warning':
      return theme.attention;
    case 'danger':
      return theme.critical;
    case 'info':
      return theme.info;
    case 'dark':
      return { fg: theme.onStrong, bg: theme.strong, solid: theme.strong };
    default:
      return theme.neutral;
  }
}
