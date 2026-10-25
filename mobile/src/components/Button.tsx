import { ActivityIndicator, Pressable, StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';

import { Icon, type IconName } from './Icon';
import { disabledOpacity, minTouchSize, radius, space, textVariants, useTheme, type Theme } from '@/theme';

export type ButtonVariant = 'primary' | 'strong' | 'secondary' | 'ghost' | 'danger' | 'dangerGhost';

type Props = {
  title: string;
  onPress: () => void;
  /** primary: sarı levha (ekranın asıl eylemi) · strong: lacivert · secondary: gri · ghost: yalın metin */
  variant?: ButtonVariant;
  size?: 'md' | 'lg';
  icon?: IconName;
  loading?: boolean;
  disabled?: boolean;
  /** Kendi genişliği kadar (varsayılan: satırı doldurur) */
  inline?: boolean;
  style?: StyleProp<ViewStyle>;
  accessibilityHint?: string;
  testID?: string;
};

function colorsFor(theme: Theme, variant: ButtonVariant) {
  switch (variant) {
    case 'primary':
      return { bg: theme.brand, pressed: theme.brandPressed, text: theme.onBrand };
    case 'strong':
      return { bg: theme.strong, pressed: theme.strongPressed, text: theme.onStrong };
    case 'secondary':
      return { bg: theme.surfaceAlt, pressed: theme.lineStrong, text: theme.text };
    case 'danger':
      return { bg: theme.critical.solid, pressed: theme.critical.fg, text: '#FFFFFF' };
    case 'dangerGhost':
      return { bg: 'transparent', pressed: theme.critical.bg, text: theme.critical.fg };
    default:
      return { bg: 'transparent', pressed: theme.surfaceAlt, text: theme.text };
  }
}

export function Button({
  title,
  onPress,
  variant = 'primary',
  size = 'md',
  icon,
  loading = false,
  disabled = false,
  inline = false,
  style,
  accessibilityHint,
  testID,
}: Props) {
  const theme = useTheme();
  const palette = colorsFor(theme, variant);
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
        size === 'lg' ? styles.lg : styles.md,
        inline ? styles.inline : null,
        { backgroundColor: pressed ? palette.pressed : palette.bg },
        pressed ? styles.pressed : null,
        disabled && !loading ? styles.disabled : null,
        style,
      ]}>
      <View style={styles.content}>
        {loading ? (
          <ActivityIndicator color={palette.text} />
        ) : icon ? (
          <Icon name={icon} size={size === 'lg' ? 22 : 20} color={palette.text} />
        ) : null}
        <Text numberOfLines={1} style={[textVariants.button, { color: palette.text }]}>
          {title}
        </Text>
      </View>
    </Pressable>
  );
}

const styles = StyleSheet.create({
  base: {
    minHeight: minTouchSize,
    alignItems: 'center',
    justifyContent: 'center',
    paddingHorizontal: space.xl,
  },
  md: {
    height: 50,
    borderRadius: radius.control,
  },
  lg: {
    height: 58,
    borderRadius: radius.control + 2,
  },
  inline: {
    alignSelf: 'flex-start',
    height: 44,
    paddingHorizontal: space.lg,
  },
  content: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'center',
    gap: space.sm,
  },
  pressed: {
    transform: [{ scale: 0.98 }],
  },
  disabled: {
    opacity: disabledOpacity,
  },
});
