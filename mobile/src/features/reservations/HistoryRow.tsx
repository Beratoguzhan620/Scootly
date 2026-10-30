import { Pressable, Text, View } from 'react-native';

import { Icon } from '@/components/Icon';
import { Plate } from '@/components/Plate';
import { StatusPill } from '@/components/StatusPill';
import { reservationOutcomeMeta } from '@/domain/labels';
import type { ReservationRecord } from '@/domain/reservationHistory';
import { formatMinutes, formatRelativeDateTime } from '@/lib/format';
import { makeStyles, radius, space, textVariants, useTheme } from '@/theme';

type Props = {
  record: ReservationRecord;
  first: boolean;
  last: boolean;
  now: number;
  /** Sürüşe dönüşen rezervasyonda sürüş özetine gider */
  onPress?: () => void;
};

/** Geçmiş rezervasyon: ne zaman, hangi scooter, nasıl bitti. */
export function HistoryRow({ record, first, last, now, onPress }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const meta = reservationOutcomeMeta(record.outcome);
  const when = formatRelativeDateTime(new Date(record.reservedAt), new Date(now));
  const held = formatMinutes(record.endedAt - record.reservedAt);

  return (
    <Pressable
      accessibilityRole={onPress ? 'button' : undefined}
      accessibilityLabel={`${when} rezervasyonu, ${record.brand ?? 'scooter'}, ${meta.label}`}
      disabled={!onPress}
      onPress={onPress}
      style={({ pressed }) => [styles.row, first && styles.first, last && styles.last, pressed && styles.pressed]}>
      <View style={styles.main}>
        <View style={styles.top}>
          <Text style={styles.when} numberOfLines={1}>
            {when}
          </Text>
          <StatusPill label={meta.label} tone={meta.tone} />
        </View>
        <View style={styles.meta}>
          <Plate id={record.vehicleId} kind="Araç" />
          <Text style={styles.detail} numberOfLines={1}>
            {record.outcome === 'ride'
              ? [record.brand, record.card].filter(Boolean).join(' · ')
              : `${record.brand ?? 'Scooter'} · ${held}`}
          </Text>
        </View>
      </View>
      {onPress ? <Icon name="chevron-forward" size={18} color={theme.textSubtle} /> : null}
    </Pressable>
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
    borderBottomWidth: 1,
    borderBottomColor: t.line,
  },
  first: {
    borderTopLeftRadius: radius.surface,
    borderTopRightRadius: radius.surface,
  },
  last: {
    borderBottomLeftRadius: radius.surface,
    borderBottomRightRadius: radius.surface,
    borderBottomWidth: 0,
  },
  pressed: {
    backgroundColor: t.surfaceAlt,
  },
  main: {
    flex: 1,
    gap: space.sm,
  },
  top: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: space.sm,
  },
  when: {
    ...textVariants.bodyStrong,
    color: t.text,
    flexShrink: 1,
  },
  meta: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
  },
  detail: {
    ...textVariants.caption,
    fontSize: 14,
    color: t.textMuted,
    flexShrink: 1,
  },
}));
