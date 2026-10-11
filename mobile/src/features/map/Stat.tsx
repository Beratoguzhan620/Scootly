import { StyleSheet, Text, View } from 'react-native';

import { colors, typography } from '@/theme';

/** Kartlarda büyük bir değer ve altında küçük açıklama (süre, ücret, geri sayım). */
export function Stat({ value, label, accessibilityLabel }: { value: string; label: string; accessibilityLabel?: string }) {
  return (
    <View style={styles.stat} accessible accessibilityLabel={accessibilityLabel ?? `${label}: ${value}`}>
      <Text style={styles.value}>{value}</Text>
      <Text style={styles.label}>{label}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  stat: {
    flex: 1,
  },
  value: {
    ...typography.h2,
    color: colors.body,
    fontVariant: ['tabular-nums'],
  },
  label: {
    ...typography.small,
    color: colors.muted,
  },
});
