import { Text, type TextProps, type TextStyle } from 'react-native';

import { textVariants, useTheme, type TextVariantName } from '@/theme';

type Props = TextProps & {
  variant?: TextVariantName;
  /** İkincil metin rengi */
  muted?: boolean;
  color?: string;
  align?: TextStyle['textAlign'];
};

/** Uygulamadaki tüm metin: yazı ailesi, ölçek ve tema rengi buradan gelir. */
export function AppText({ variant = 'body', muted = false, color, align, style, ...rest }: Props) {
  const theme = useTheme();
  const headingRole = variant === 'largeTitle' || variant === 'title';
  return (
    <Text
      accessibilityRole={headingRole ? 'header' : undefined}
      style={[
        textVariants[variant],
        { color: color ?? (muted ? theme.textMuted : theme.text) },
        align ? { textAlign: align } : null,
        style,
      ]}
      {...rest}
    />
  );
}
