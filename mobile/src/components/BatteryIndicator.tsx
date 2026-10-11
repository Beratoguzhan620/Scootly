import Ionicons from '@expo/vector-icons/Ionicons';
import { StyleSheet, Text, View } from 'react-native';

import { LOW_BATTERY_THRESHOLD, MIN_RENTABLE_BATTERY } from '@/domain/rules';
import { colors, typography } from '@/theme';

export function batteryColor(percentage: number): string {
  if (percentage < MIN_RENTABLE_BATTERY) return colors.danger;
  if (percentage < LOW_BATTERY_THRESHOLD) return '#cc9a06'; // warning tonunun beyaz zeminde okunur hali
  return colors.success;
}

function batteryIcon(percentage: number): keyof typeof Ionicons.glyphMap {
  if (percentage >= 70) return 'battery-full';
  if (percentage >= LOW_BATTERY_THRESHOLD) return 'battery-half';
  return 'battery-dead';
}

/** Batarya yüzdesi — panel gibi "%78" biçiminde. */
export function BatteryIndicator({ percentage }: { percentage: number }) {
  const color = batteryColor(percentage);
  return (
    <View style={styles.row} accessibilityLabel={`Batarya yüzde ${percentage}`}>
      <Ionicons name={batteryIcon(percentage)} size={20} color={color} />
      <Text style={[styles.text, { color }]}>%{percentage}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 4,
  },
  text: {
    ...typography.body,
    fontWeight: '500',
    color: colors.body,
  },
});
