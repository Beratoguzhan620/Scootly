import type { ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

import { Icon, type IconName } from './Icon';
import { makeStyles, radius, space, textVariants, toneColors, useTheme, type Theme } from '@/theme';

export type NoticeTone = 'info' | 'success' | 'warning' | 'danger' | 'brand';

type Props = {
  tone?: NoticeTone;
  title?: string;
  children: ReactNode;
  /** Kapatma düğmesi */
  onDismiss?: () => void;
  /** Mesajın altında metin düğmesi: "Tekrar dene", "Ayarları aç" */
  action?: { label: string; onPress: () => void };
};

const icons: Record<NoticeTone, IconName> = {
  info: 'information-circle',
  success: 'checkmark-circle',
  warning: 'warning',
  danger: 'alert-circle',
  brand: 'flash',
};

function colorsFor(theme: Theme, tone: NoticeTone) {
  switch (tone) {
    case 'success':
      return toneColors(theme, 'success');
    case 'warning':
      return toneColors(theme, 'warning');
    case 'danger':
      return toneColors(theme, 'danger');
    case 'brand':
      return toneColors(theme, 'primary');
    default:
      return toneColors(theme, 'info');
  }
}

/** Ekran içi bilgi, uyarı ve hata mesajı. Hatalar özür dilemez; ne olduğunu ve ne yapılacağını söyler. */
export function Notice({ tone = 'info', title, children, onDismiss, action }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const colors = colorsFor(theme, tone);
  const important = tone === 'danger' || tone === 'warning';

  return (
    <View
      style={[styles.box, { backgroundColor: colors.bg }]}
      accessibilityRole={important ? 'alert' : undefined}
      accessibilityLiveRegion={important ? 'polite' : 'none'}>
      <View style={styles.icon}>
        <Icon name={icons[tone]} size={20} color={tone === 'brand' ? colors.fg : colors.solid} />
      </View>
      <View style={styles.body}>
        {title ? <Text style={[styles.title, { color: colors.fg }]}>{title}</Text> : null}
        {typeof children === 'string' ? (
          <Text style={[styles.text, { color: title ? theme.text : colors.fg }]}>{children}</Text>
        ) : (
          children
        )}
        {action ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={action.label}
            hitSlop={8}
            onPress={action.onPress}
            style={styles.action}>
            <Text style={[styles.actionText, { color: colors.fg }]}>{action.label}</Text>
          </Pressable>
        ) : null}
      </View>
      {onDismiss ? (
        <Pressable
          accessibilityRole="button"
          accessibilityLabel="Kapat"
          hitSlop={10}
          onPress={onDismiss}
          style={styles.dismiss}>
          <Icon name="close" size={20} color={colors.fg} />
        </Pressable>
      ) : null}
    </View>
  );
}

const useStyles = makeStyles(() => ({
  box: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: space.md,
    borderRadius: radius.control,
    paddingVertical: space.md,
    paddingLeft: space.md + 2,
    paddingRight: space.md,
  },
  icon: {
    paddingTop: 1,
  },
  body: {
    flex: 1,
    gap: space.xs,
  },
  title: {
    ...textVariants.bodyStrong,
  },
  text: {
    ...textVariants.body,
    fontSize: 15,
    lineHeight: 21,
  },
  action: {
    alignSelf: 'flex-start',
    paddingVertical: space.xs,
  },
  actionText: {
    ...textVariants.bodyStrong,
    fontSize: 15,
    textDecorationLine: 'underline',
  },
  dismiss: {
    width: 28,
    height: 28,
    alignItems: 'center',
    justifyContent: 'center',
  },
}));
