import type { ReactNode } from 'react';
import { ActivityIndicator, Pressable, StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';

import { colors, disabledOpacity, radius, typography } from '@/theme';

type VariantStyle = {
  bg: string;
  border: string;
  text: string;
  pressedBg: string;
  pressedBorder: string;
  pressedText?: string;
  underline?: boolean;
};

const solid = (bg: string, pressedBg: string, pressedBorder: string, text: string = colors.white): VariantStyle => ({
  bg,
  border: bg,
  text,
  pressedBg,
  pressedBorder,
});

const outline = (color: string): VariantStyle => ({
  bg: 'transparent',
  border: color,
  text: color,
  pressedBg: color,
  pressedBorder: color,
  pressedText: colors.white,
});

/** Bootstrap 5.3.3 buton renkleri; basılı hali `:active` durumunun renkleridir. */
const variants = {
  primary: solid(colors.primary, '#0a58ca', '#0a53be'),
  secondary: solid(colors.secondary, '#565e64', '#51585e'),
  success: solid(colors.success, '#146c43', '#13653f'),
  danger: solid(colors.danger, '#b02a37', '#a52834'),
  light: solid(colors.light, '#c6c7c8', '#babbbc', colors.black),
  'outline-primary': outline(colors.primary),
  'outline-secondary': outline(colors.secondary),
  'outline-success': outline(colors.success),
  'outline-danger': outline(colors.danger),
  link: {
    bg: 'transparent',
    border: 'transparent',
    text: colors.link,
    pressedBg: 'transparent',
    pressedBorder: 'transparent',
    pressedText: '#0a58ca',
    underline: true,
  },
} satisfies Record<string, VariantStyle>;

export type ButtonVariant = keyof typeof variants;

type Props = {
  title: string;
  onPress?: () => void;
  variant?: ButtonVariant;
  size?: 'sm' | 'md' | 'lg';
  loading?: boolean;
  disabled?: boolean;
  /** Simge, butonun o anki yazı rengiyle çizilir. */
  icon?: (color: string) => ReactNode;
  style?: StyleProp<ViewStyle>;
  accessibilityHint?: string;
  testID?: string;
};

export function Button({
  title,
  onPress,
  variant = 'primary',
  size = 'md',
  loading = false,
  disabled = false,
  icon,
  style,
  accessibilityHint,
  testID,
}: Props) {
  const palette: VariantStyle = variants[variant];
  const inactive = disabled || loading;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={title}
      accessibilityHint={accessibilityHint}
      accessibilityState={{ disabled: inactive, busy: loading }}
      disabled={inactive}
      onPress={onPress}
      testID={testID}
      style={({ pressed }) => [
        styles.base,
        sizes[size],
        {
          backgroundColor: pressed ? palette.pressedBg : palette.bg,
          borderColor: pressed ? palette.pressedBorder : palette.border,
        },
        inactive && { opacity: disabledOpacity },
        style,
      ]}>
      {({ pressed }) => {
        const textColor = pressed && palette.pressedText ? palette.pressedText : palette.text;
        return (
          <View style={styles.content}>
            {loading ? <ActivityIndicator size="small" color={textColor} /> : icon?.(textColor)}
            <Text
              numberOfLines={1}
              style={[styles.label, labelSizes[size], { color: textColor }, palette.underline && styles.underline]}>
              {title}
            </Text>
          </View>
        );
      }}
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    borderWidth: 1,
    alignItems: 'center',
    justifyContent: 'center',
  },
  content: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: 8,
  },
  label: {
    fontWeight: '400',
    textAlign: 'center',
  },
  underline: {
    textDecorationLine: 'underline',
  },
});

// Bootstrap ölçüleri (btn-sm / btn / btn-lg), yükseklikler dokunma hedefine göre büyütüldü.
const sizes = StyleSheet.create({
  sm: { minHeight: 34, paddingHorizontal: 8, borderRadius: radius.sm },
  md: { minHeight: 44, paddingHorizontal: 12, borderRadius: radius.md },
  lg: { minHeight: 52, paddingHorizontal: 16, borderRadius: radius.lg },
});

const labelSizes = StyleSheet.create({
  sm: { fontSize: typography.small.fontSize },
  md: { fontSize: typography.body.fontSize },
  lg: { fontSize: 18 },
});
