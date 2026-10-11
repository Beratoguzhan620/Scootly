import { StyleSheet, View } from 'react-native';

import { colors, radius } from '@/theme';

type Props = {
  /** 0–1 arası */
  value: number;
  color?: string;
  accessibilityLabel?: string;
};

/** Bootstrap `.progress` */
export function ProgressBar({ value, color = colors.primary, accessibilityLabel }: Props) {
  const clamped = Math.min(1, Math.max(0, value));
  return (
    <View
      style={styles.track}
      accessibilityRole="progressbar"
      accessibilityLabel={accessibilityLabel}
      accessibilityValue={{ min: 0, max: 100, now: Math.round(clamped * 100) }}>
      <View style={[styles.bar, { width: `${clamped * 100}%`, backgroundColor: color }]} />
    </View>
  );
}

const styles = StyleSheet.create({
  track: {
    height: 8,
    borderRadius: radius.md,
    backgroundColor: colors.secondaryBg,
    overflow: 'hidden',
  },
  bar: {
    height: '100%',
    borderRadius: radius.md,
  },
});
