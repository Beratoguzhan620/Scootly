import type { ReactNode } from 'react';
import { ActivityIndicator, View } from 'react-native';

import { AppText } from './AppText';
import { Icon, type IconName } from './Icon';
import { makeStyles, space, useTheme } from '@/theme';

export function LoadingView({ message = 'Yükleniyor…' }: { message?: string }) {
  const theme = useTheme();
  const styles = useStyles();
  return (
    <View style={styles.center} accessibilityLiveRegion="polite">
      <ActivityIndicator color={theme.text} />
      <AppText muted>{message}</AppText>
    </View>
  );
}

type EmptyProps = {
  icon: IconName;
  title: string;
  message?: string;
  /** Boş ekranda yapılacak ilk iş (örn. haritaya dönmek) */
  action?: ReactNode;
};

/** Boş liste: ne olduğunu söyler ve ilk adımı gösterir. */
export function EmptyState({ icon, title, message, action }: EmptyProps) {
  const theme = useTheme();
  const styles = useStyles();
  return (
    <View style={styles.empty}>
      <View style={styles.iconDisc}>
        <Icon name={icon} size={30} color={theme.onBrand} />
      </View>
      <AppText variant="heading" align="center">
        {title}
      </AppText>
      {message ? (
        <AppText muted align="center" style={styles.message}>
          {message}
        </AppText>
      ) : null}
      {action ? <View style={styles.action}>{action}</View> : null}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  center: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: space.md,
    padding: space.xxxl,
    backgroundColor: t.bg,
  },
  empty: {
    alignItems: 'center',
    gap: space.sm,
    paddingVertical: space.xxxl * 1.5,
    paddingHorizontal: space.xl,
  },
  iconDisc: {
    width: 64,
    height: 64,
    borderRadius: 32,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.brand,
    marginBottom: space.sm,
  },
  message: {
    maxWidth: 300,
  },
  action: {
    marginTop: space.md,
    alignSelf: 'stretch',
    alignItems: 'center',
  },
}));
