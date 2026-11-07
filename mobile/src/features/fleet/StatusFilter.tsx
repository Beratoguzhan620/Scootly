import { Pressable, ScrollView, Text, View } from 'react-native';

import type { VehicleStatus } from '@/api/types';
import { vehicleStatusMeta } from '@/domain/labels';
import { VEHICLE_STATUSES, type StatusCounts } from '@/domain/fleet';
import { vehicleStatusColor } from '@/features/map/mapTheme';
import { haptics } from '@/lib/haptics';
import { makeStyles, radius, space, textVariants, useTheme } from '@/theme';

type Props = {
  counts: StatusCounts;
  value: VehicleStatus | null;
  onChange: (value: VehicleStatus | null) => void;
  /** Harita üstünde: gölgeli, beyaz zeminli çipler */
  floating?: boolean;
  /** Kaydırılan satırın kenar boşluğu (ekranın kenar boşluğuyla hizalanır) */
  inset?: number;
};

/**
 * Duruma göre süzme. Her çipteki nokta, haritadaki işaretin rengidir; çipler aynı zamanda harita açıklamasıdır.
 */
export function StatusFilter({ counts, value, onChange, floating = false, inset = space.lg }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const items: { key: VehicleStatus | null; label: string; count: number }[] = [
    { key: null, label: 'Tümü', count: counts.total },
    ...VEHICLE_STATUSES.map((status) => ({ key: status, label: vehicleStatusMeta(status).label, count: counts[status] })),
  ];

  return (
    <ScrollView
      horizontal
      showsHorizontalScrollIndicator={false}
      contentContainerStyle={[styles.row, { paddingHorizontal: inset }]}
      accessibilityRole="tablist">
      {items.map((item) => {
        const selected = item.key === value;
        return (
          <Pressable
            key={item.key ?? 'all'}
            accessibilityRole="tab"
            accessibilityState={{ selected }}
            accessibilityLabel={`${item.label}, ${item.count} araç`}
            onPress={() => {
              haptics.select();
              onChange(selected && item.key !== null ? null : item.key);
            }}
            style={({ pressed }) => [
              styles.chip,
              floating ? styles.floating : styles.flat,
              selected && styles.selected,
              pressed && styles.pressed,
            ]}>
            {item.key ? (
              <View
                style={[
                  styles.dot,
                  { backgroundColor: vehicleStatusColor(item.key, theme.scheme) },
                  selected && styles.dotSelected,
                ]}
              />
            ) : null}
            <Text style={[styles.label, selected && styles.labelSelected]}>{item.label}</Text>
            <Text style={[styles.count, selected && styles.labelSelected]}>{item.count}</Text>
          </Pressable>
        );
      })}
    </ScrollView>
  );
}

const useStyles = makeStyles((t) => ({
  row: {
    gap: space.sm,
    paddingVertical: space.xs,
  },
  chip: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.xs + 2,
    height: 38,
    paddingHorizontal: space.md + 2,
    borderRadius: radius.pill,
  },
  floating: {
    backgroundColor: t.surface,
    boxShadow: t.floatShadow,
  },
  flat: {
    backgroundColor: t.surface,
    borderWidth: 1,
    borderColor: t.line,
  },
  selected: {
    backgroundColor: t.strong,
    borderColor: t.strong,
  },
  pressed: {
    opacity: 0.8,
  },
  dot: {
    width: 10,
    height: 10,
    borderRadius: 5,
  },
  dotSelected: {
    borderWidth: 1.5,
    borderColor: t.onStrong,
  },
  label: {
    ...textVariants.label,
    color: t.text,
  },
  count: {
    ...textVariants.label,
    fontFamily: textVariants.numeral.fontFamily,
    color: t.textMuted,
  },
  labelSelected: {
    color: t.onStrong,
  },
}));
