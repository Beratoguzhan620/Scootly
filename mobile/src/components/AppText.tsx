import { StyleSheet, Text, type TextProps } from 'react-native';

import { colors, typography } from '@/theme';

type Variant = 'h1' | 'h2' | 'h3' | 'h4' | 'h5' | 'h6' | 'lead' | 'body' | 'small';

type Props = TextProps & {
  variant?: Variant;
  /** `.text-muted` */
  muted?: boolean;
  color?: string;
  weight?: '300' | '400' | '500' | '700';
  align?: 'left' | 'center' | 'right';
};

const headings: ReadonlySet<Variant> = new Set(['h1', 'h2', 'h3', 'h4', 'h5', 'h6']);

export function AppText({ variant = 'body', muted, color, weight, align, style, ...rest }: Props) {
  return (
    <Text
      accessibilityRole={headings.has(variant) ? 'header' : undefined}
      style={[
        typography[variant],
        { color: color ?? (muted ? colors.muted : colors.body) },
        weight ? { fontWeight: weight } : null,
        align ? { textAlign: align } : null,
        style,
      ]}
      {...rest}
    />
  );
}

/** Paneldeki `<code>` görünümü — kısa kimlikler için. */
export function Code({ style, ...rest }: TextProps) {
  return <Text style={[styles.code, style]} {...rest} />;
}

const styles = StyleSheet.create({
  code: {
    fontFamily: typography.monospaceFamily,
    fontSize: 14,
    color: colors.code,
  },
});
