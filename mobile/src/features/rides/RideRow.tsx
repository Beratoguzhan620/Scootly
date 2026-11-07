import { Pressable, Text, View } from 'react-native';

import type { Ride } from '@/api/types';
import { Plate } from '@/components/Plate';
import { PaymentStatusPill, RideStatusPill } from '@/components/StatusPill';
import { formatCurrency, formatMinutes, formatRelativeDateTime, parseServerDate } from '@/lib/format';
import { makeStyles, radius, space, textVariants } from '@/theme';

type Props = {
  ride: Ride;
  /** Ay grubunun ilk ve son satırı: köşeler yuvarlanır. */
  first: boolean;
  last: boolean;
  now: number;
  onPress: () => void;
};

/** Sürüş geçmişi satırı: ne zaman, ne kadar sürdü, hangi scooter; sağda ücret ve ödeme durumu. */
export function RideRow({ ride, first, last, now, onPress }: Props) {
  const styles = useStyles();
  const startedAt = parseServerDate(ride.startedAt);
  const endedAt = ride.endedAt ? parseServerDate(ride.endedAt).getTime() : now;
  const duration = formatMinutes(endedAt - startedAt.getTime());
  const when = formatRelativeDateTime(startedAt, new Date(now));

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${when} sürüşü, ${duration}${ride.fare !== null ? `, ${formatCurrency(ride.fare)}` : ''}`}
      onPress={onPress}
      style={({ pressed }) => [styles.row, first && styles.first, last && styles.last, pressed && styles.pressed]}>
      <View style={styles.main}>
        <Text style={styles.when}>{when}</Text>
        <View style={styles.meta}>
          <Text style={styles.duration}>{duration}</Text>
          <Plate id={ride.vehicleId} kind="Araç" />
        </View>
      </View>
      <View style={styles.side}>
        <Text style={styles.fare}>{ride.fare !== null ? formatCurrency(ride.fare) : '—'}</Text>
        {ride.status === 'Completed' ? (
          <PaymentStatusPill status={ride.paymentStatus} />
        ) : (
          <RideStatusPill status={ride.status} />
        )}
      </View>
    </Pressable>
  );
}

export function RideRowSeparator() {
  const styles = useStyles();
  return (
    <View style={styles.separatorTrack}>
      <View style={styles.separator} />
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
    paddingVertical: space.md + 2,
    paddingHorizontal: space.lg + 2,
    backgroundColor: t.surface,
  },
  first: {
    borderTopLeftRadius: radius.surface,
    borderTopRightRadius: radius.surface,
  },
  last: {
    borderBottomLeftRadius: radius.surface,
    borderBottomRightRadius: radius.surface,
  },
  pressed: {
    backgroundColor: t.surfaceAlt,
  },
  main: {
    flex: 1,
    gap: space.xs + 2,
  },
  when: {
    ...textVariants.bodyStrong,
    color: t.text,
  },
  meta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
  },
  duration: {
    ...textVariants.caption,
    fontSize: 14,
    color: t.textMuted,
  },
  side: {
    alignItems: 'flex-end',
    gap: space.xs + 2,
  },
  fare: {
    ...textVariants.numeral,
    fontSize: 22,
    lineHeight: 26,
    color: t.text,
  },
  separatorTrack: {
    backgroundColor: t.surface,
  },
  separator: {
    height: 1,
    backgroundColor: t.line,
    marginLeft: space.lg + 2,
  },
}));
