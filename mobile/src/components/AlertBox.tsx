import type { ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';

import { radius, spacing, subtle, typography } from '@/theme';

type AlertTone = keyof typeof subtle;

type Props = {
  tone: AlertTone;
  title?: string;
  children: ReactNode;
  /** Paneldeki `alert-dismissible` + `btn-close` */
  onDismiss?: () => void;
  /** `.alert-link` görünümünde bir eylem */
  action?: { label: string; onPress: () => void };
  style?: StyleProp<ViewStyle>;
};

/** Bootstrap `.alert-*` — panelin TempData başarı/hata mesajlarıyla aynı görünüm. */
export function AlertBox({ tone, title, children, onDismiss, action, style }: Props) {
  const palette = subtle[tone];
  const isAssertive = tone === 'danger' || tone === 'warning';

  return (
    <View
      accessibilityRole="alert"
      accessibilityLiveRegion={isAssertive ? 'assertive' : 'polite'}
      style={[styles.alert, { backgroundColor: palette.bg, borderColor: palette.border }, style]}>
      <View style={styles.content}>
        {title ? <Text style={[styles.title, { color: palette.text }]}>{title}</Text> : null}
        {typeof children === 'string' ? (
          <Text style={[styles.message, { color: palette.text }]}>{children}</Text>
        ) : (
          children
        )}
        {action ? (
          <Text
            accessibilityRole="button"
            onPress={action.onPress}
            style={[styles.link, { color: palette.text }]}
            suppressHighlighting>
            {action.label}
          </Text>
        ) : null}
      </View>
      {onDismiss ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Kapat"
          hitSlop={12}
          onPress={onDismiss}
          style={styles.close}>
          <Text style={[styles.closeText, { color: palette.text }]}>×</Text>
        </Pressable>
      ) : null}
    </View>
  );
}

const styles = StyleSheet.create({
  alert: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    padding: spacing[3],
    borderWidth: 1,
    borderRadius: radius.md,
  },
  content: {
    flex: 1,
    gap: spacing[1],
  },
  title: {
    ...typography.body,
    fontWeight: '700',
  },
  message: {
    ...typography.body,
  },
  link: {
    ...typography.body,
    fontWeight: '700',
    textDecorationLine: 'underline',
    marginTop: spacing[1],
  },
  close: {
    marginLeft: spacing[2],
    opacity: 0.5,
  },
  closeText: {
    fontSize: 22,
    lineHeight: 22,
  },
});
