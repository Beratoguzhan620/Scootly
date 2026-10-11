import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { colors, spacing, typography } from '@/theme';

export function LoadingView({ message = 'Yükleniyor...' }: { message?: string }) {
  return (
    <View style={styles.center} accessibilityLiveRegion="polite">
      <ActivityIndicator color={colors.primary} />
      <Text style={styles.muted}>{message}</Text>
    </View>
  );
}

/** Paneldeki `text-center py-5 text-muted` boş durum. */
export function EmptyState({ message }: { message: string }) {
  return (
    <View style={styles.empty}>
      <Text style={[styles.muted, styles.centerText]}>{message}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  center: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: spacing[2],
    padding: spacing[4],
  },
  empty: {
    paddingVertical: spacing[5],
    paddingHorizontal: spacing[3],
  },
  muted: {
    ...typography.body,
    color: colors.muted,
  },
  centerText: {
    textAlign: 'center',
  },
});
