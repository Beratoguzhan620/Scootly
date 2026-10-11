import { StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';

import { paymentStatusMeta, rideStatusMeta, vehicleStatusMeta } from '@/domain/labels';
import { colors, radius, typography, type Tone } from '@/theme';

// Panel `bg-warning text-dark` ve `bg-light text-dark` kullanıyor; açık zeminli tonlarda yazı koyu.
const toneColors: Record<Tone, { bg: string; text: string }> = {
  primary: { bg: colors.primary, text: colors.white },
  secondary: { bg: colors.secondary, text: colors.white },
  success: { bg: colors.success, text: colors.white },
  danger: { bg: colors.danger, text: colors.white },
  warning: { bg: colors.warning, text: colors.dark },
  info: { bg: colors.info, text: colors.dark },
  light: { bg: colors.light, text: colors.dark },
  dark: { bg: colors.dark, text: colors.white },
};

type Props = {
  label: string;
  tone?: Tone;
  style?: StyleProp<ViewStyle>;
};

/** Bootstrap `.badge` */
export function Badge({ label, tone = 'secondary', style }: Props) {
  const palette = toneColors[tone];
  return (
    <View style={[styles.badge, { backgroundColor: palette.bg }, style]}>
      <Text style={[styles.text, { color: palette.text }]}>{label}</Text>
    </View>
  );
}

export function VehicleStatusBadge({ status }: { status: string }) {
  const meta = vehicleStatusMeta(status);
  return <Badge label={meta.label} tone={meta.tone} />;
}

export function RideStatusBadge({ status }: { status: string }) {
  const meta = rideStatusMeta(status);
  return <Badge label={meta.label} tone={meta.tone} />;
}

export function PaymentStatusBadge({ status }: { status: string }) {
  const meta = paymentStatusMeta(status);
  return <Badge label={meta.label} tone={meta.tone} />;
}

const styles = StyleSheet.create({
  badge: {
    alignSelf: 'flex-start',
    paddingHorizontal: 8,
    paddingVertical: 4,
    borderRadius: radius.md,
  },
  text: {
    ...typography.badge,
  },
});
