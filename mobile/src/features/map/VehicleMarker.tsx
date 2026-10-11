import { StyleSheet, Text, View } from 'react-native';

import { vehicleStatusMeta } from '@/domain/labels';
import { colors, type Tone } from '@/theme';

const fill: Partial<Record<Tone, { bg: string; text: string }>> = {
  success: { bg: colors.success, text: colors.white },
  warning: { bg: colors.warning, text: colors.dark },
  primary: { bg: colors.primary, text: colors.white },
  secondary: { bg: colors.secondary, text: colors.white },
  danger: { bg: colors.danger, text: colors.white },
};

type Props = {
  status: string;
  battery: number;
  selected: boolean;
};

/**
 * Paneldeki harita işaretinin (durum renginde, beyaz kenarlı nokta) mobil hali: dokunması kolay olsun diye
 * içinde batarya yüzdesi olan bir hap.
 */
export function VehicleMarker({ status, battery, selected }: Props) {
  const palette = fill[vehicleStatusMeta(status).tone] ?? { bg: colors.secondary, text: colors.white };
  return (
    <View style={[styles.pin, { backgroundColor: palette.bg }, selected && styles.selected]}>
      <Text style={[styles.text, { color: palette.text }, selected && styles.selectedText]}>%{battery}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  pin: {
    minWidth: 44,
    height: 26,
    paddingHorizontal: 8,
    borderRadius: 13,
    borderWidth: 2,
    borderColor: colors.white,
    alignItems: 'center',
    justifyContent: 'center',
  },
  selected: {
    height: 32,
    borderRadius: 16,
    borderWidth: 3,
    borderColor: colors.dark,
  },
  text: {
    fontSize: 12,
    fontWeight: '700',
  },
  selectedText: {
    fontSize: 14,
  },
});
