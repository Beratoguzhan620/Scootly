import { Pressable, StyleSheet, View } from 'react-native';

import type { Ride } from '@/api/types';
import { AppText, Code } from '@/components/AppText';
import { PaymentStatusBadge, RideStatusBadge } from '@/components/Badge';
import { formatCurrency, formatDateTime, formatMinutes, parseServerDate, shortId } from '@/lib/format';
import { colors, radius, spacing } from '@/theme';

type Props = {
  ride: Ride;
  first: boolean;
  last: boolean;
  now: number;
  onPress: () => void;
};

/** Sürüş geçmişi satırı — FlatList içinde tek parça bir `list-group` gibi görünür. */
export function RideRow({ ride, first, last, now, onPress }: Props) {
  const startedAt = parseServerDate(ride.startedAt);
  const endedAt = ride.endedAt ? parseServerDate(ride.endedAt).getTime() : now;
  const duration = formatMinutes(endedAt - startedAt.getTime());

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${formatDateTime(startedAt)} sürüşü, ${duration}`}
      onPress={onPress}
      style={({ pressed }) => [styles.row, first && styles.first, last && styles.last, pressed && styles.pressed]}>
      <View style={styles.main}>
        <AppText>{formatDateTime(startedAt)}</AppText>
        <AppText variant="small" muted>
          {duration} · Araç <Code>{shortId(ride.vehicleId)}</Code>
        </AppText>
      </View>
      <View style={styles.side}>
        <AppText weight="500">{ride.fare !== null ? formatCurrency(ride.fare) : '—'}</AppText>
        {ride.status === 'Completed' ? (
          <PaymentStatusBadge status={ride.paymentStatus} />
        ) : (
          <RideStatusBadge status={ride.status} />
        )}
      </View>
    </Pressable>
  );
}

export function RideRowSeparator() {
  return <View style={styles.separator} />;
}

const styles = StyleSheet.create({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing[3],
    paddingVertical: 12,
    paddingHorizontal: spacing[3],
    backgroundColor: colors.bodyBg,
    borderLeftWidth: 1,
    borderRightWidth: 1,
    borderColor: colors.borderTranslucent,
  },
  first: {
    borderTopWidth: 1,
    borderTopLeftRadius: radius.md,
    borderTopRightRadius: radius.md,
  },
  last: {
    borderBottomWidth: 1,
    borderBottomLeftRadius: radius.md,
    borderBottomRightRadius: radius.md,
  },
  pressed: {
    backgroundColor: colors.tertiaryBg,
  },
  main: {
    flex: 1,
    gap: 2,
  },
  side: {
    alignItems: 'flex-end',
    gap: spacing[1],
  },
  separator: {
    height: StyleSheet.hairlineWidth,
    backgroundColor: colors.borderTranslucent,
  },
});
