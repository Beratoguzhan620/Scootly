import type { TextStyle } from 'react-native';

/**
 * Yazı aileleri. Barlow, Kaliforniya karayolu tabelalarından türetilmiş bir grotesk; arayüz metni onunla.
 * Barlow Condensed rakamlar (batarya, süre, ücret), büyük başlıklar ve logo için.
 *
 * Android'de özel yazı tipiyle `fontWeight` birlikte kullanılınca sistem yazısına düşebildiği için her
 * kalınlık ayrı bir aile adıyla yüklenir (`fonts.ts`) ve stillerde `fontWeight` verilmez.
 */
export const fontFamily = {
  regular: 'Barlow-Regular',
  medium: 'Barlow-Medium',
  semibold: 'Barlow-SemiBold',
  bold: 'Barlow-Bold',
  condensedSemibold: 'BarlowCondensed-SemiBold',
  condensedBold: 'BarlowCondensed-Bold',
  wordmark: 'BarlowCondensed-ExtraBoldItalic',
} as const;

type TextVariant = Pick<TextStyle, 'fontFamily' | 'fontSize' | 'lineHeight' | 'letterSpacing' | 'fontVariant'>;

/** Ölçek: 13 · 15 · 16 · 18 · 22 · 36 · 56 (rakamlar ve büyük başlıklar dar yazıyla). */
export const textVariants = {
  /** Ücret ve sayaç gibi ekranın asıl sayısı */
  display: {
    fontFamily: fontFamily.condensedBold,
    fontSize: 56,
    lineHeight: 60,
    letterSpacing: -0.5,
    fontVariant: ['tabular-nums'],
  },
  /** Kartlardaki büyük değerler */
  numeral: {
    fontFamily: fontFamily.condensedSemibold,
    fontSize: 34,
    lineHeight: 38,
    fontVariant: ['tabular-nums'],
  },
  /** Sekme ekranlarının büyük başlığı */
  largeTitle: { fontFamily: fontFamily.condensedBold, fontSize: 36, lineHeight: 40, letterSpacing: -0.2 },
  title: { fontFamily: fontFamily.semibold, fontSize: 22, lineHeight: 28 },
  heading: { fontFamily: fontFamily.semibold, fontSize: 18, lineHeight: 24 },
  body: { fontFamily: fontFamily.regular, fontSize: 16, lineHeight: 23 },
  bodyStrong: { fontFamily: fontFamily.semibold, fontSize: 16, lineHeight: 23 },
  label: { fontFamily: fontFamily.medium, fontSize: 15, lineHeight: 20 },
  caption: { fontFamily: fontFamily.regular, fontSize: 13, lineHeight: 18 },
  captionStrong: { fontFamily: fontFamily.medium, fontSize: 13, lineHeight: 18 },
  button: { fontFamily: fontFamily.semibold, fontSize: 17, lineHeight: 22 },
} satisfies Record<string, TextVariant>;

export type TextVariantName = keyof typeof textVariants;
