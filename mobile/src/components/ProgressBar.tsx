import { StyleSheet, View } from 'react-native';

import { radius, useTheme } from '@/theme';

type Props = {
  /** 0–1 arası */
  value: number;
  color?: string;
  trackColor?: string;
  accessibilityLabel?: string;
};

export function ProgressBar({ value, color, trackColor, accessibilityLabel }: Props) {
  const theme = useTheme();
  const clamped = Math.min(1, Math.max(0, value));
  return (
    <View
      style={[styles.track, { backgroundColor: trackColor ?? theme.surfaceAlt }]}
      accessibilityRole="progressbar"
      accessibilityLabel={accessibilityLabel}
      accessibilityValue={{ min: 0, max: 100, now: Math.round(clamped * 100) }}>
      <View style={[styles.bar, { width: `${clamped * 100}%`, backgroundColor: color ?? theme.text }]} />
    </View>
  );
}

const styles = StyleSheet.create({
  track: {
    height: 6,
    borderRadius: radius.pill,
    overflow: 'hidden',
  },
  bar: {
    height: '100%',
    borderRadius: radius.pill,
  },
});
