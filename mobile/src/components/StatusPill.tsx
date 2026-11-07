import { StyleSheet, Text, View } from 'react-native';

import { paymentStatusMeta, rideStatusMeta, vehicleStatusMeta } from '@/domain/labels';
import { radius, space, textVariants, toneColors, useTheme, type Tone } from '@/theme';

/** Durum rozeti: renkli nokta ve kısa etiket. Renk tek başına anlam taşımaz; etiket her zaman yazılı. */
export function StatusPill({ label, tone, inverse = false }: { label: string; tone: Tone; inverse?: boolean }) {
  const theme = useTheme();
  const colors = toneColors(theme, tone);
  const dot = tone === 'primary' ? (inverse ? theme.ride.accent : theme.text) : colors.solid;

  return (
    <View
      style={[styles.pill, { backgroundColor: inverse ? theme.ride.line : colors.bg }]}
      accessible
      accessibilityLabel={label}>
      <View style={[styles.dot, { backgroundColor: dot }]} />
      <Text style={[textVariants.captionStrong, { color: inverse ? theme.ride.text : colors.fg }]}>{label}</Text>
    </View>
  );
}

export function VehicleStatusPill({ status }: { status: string }) {
  const meta = vehicleStatusMeta(status);
  return <StatusPill label={meta.label} tone={meta.tone} />;
}

export function RideStatusPill({ status, inverse }: { status: string; inverse?: boolean }) {
  const meta = rideStatusMeta(status);
  return <StatusPill label={meta.label} tone={meta.tone} inverse={inverse} />;
}

export function PaymentStatusPill({ status }: { status: string }) {
  const meta = paymentStatusMeta(status);
  return <StatusPill label={meta.label} tone={meta.tone} />;
}

const styles = StyleSheet.create({
  pill: {
    flexDirection: 'row',
    alignItems: 'center',
    alignSelf: 'flex-start',
    gap: space.xs + 2,
    borderRadius: radius.pill,
    paddingHorizontal: space.sm + 2,
    paddingVertical: 3,
  },
  dot: {
    width: 7,
    height: 7,
    borderRadius: 4,
  },
});
