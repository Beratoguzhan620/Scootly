import { Pressable, Text, View } from 'react-native';

import type { Vehicle } from '@/api/types';
import { Icon } from '@/components/Icon';
import { Plate } from '@/components/Plate';
import { VehicleStatusPill } from '@/components/StatusPill';
import { batteryLevel, batteryPercent } from '@/domain/battery';
import { vehicleStatusMeta } from '@/domain/labels';
import { vehicleStatusColor } from '@/features/map/mapTheme';
import { makeStyles, radius, space, textVariants, useTheme } from '@/theme';

type Props = {
  vehicle: Vehicle;
  first: boolean;
  last: boolean;
  onPress: () => void;
};

/** Araç listesi satırı: soldaki şerit haritadaki işaretin rengi; plaka, marka, batarya ve durum. */
export function FleetVehicleRow({ vehicle, first, last, onPress }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const battery = batteryPercent(vehicle.batteryPercentage);
  const low = batteryLevel(battery) !== 'ok';

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={`${vehicle.brand}, ${vehicleStatusMeta(vehicle.status).label}, batarya yüzde ${battery}`}
      onPress={onPress}
      style={({ pressed }) => [styles.row, first && styles.first, last && styles.last, pressed && styles.pressed]}>
      <View style={[styles.stripe, { backgroundColor: vehicleStatusColor(vehicle.status, theme.scheme) }]} />
      <View style={styles.main}>
        <View style={styles.titleRow}>
          <Plate id={vehicle.id} kind="Araç" />
          <Text style={styles.brand} numberOfLines={1}>
            {vehicle.brand}
          </Text>
        </View>
        <VehicleStatusPill status={vehicle.status} />
      </View>
      <View style={styles.battery}>
        <Text style={[styles.batteryValue, low && { color: theme.critical.fg }]}>{`%${battery}`}</Text>
        <Text style={styles.batteryLabel}>batarya</Text>
      </View>
      <Icon name="chevron-forward" size={18} color={theme.textSubtle} />
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
    paddingVertical: space.md,
    paddingRight: space.lg,
    backgroundColor: t.surface,
    borderBottomWidth: 1,
    borderBottomColor: t.line,
    overflow: 'hidden',
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
  stripe: {
    width: 5,
    alignSelf: 'stretch',
    borderTopRightRadius: 3,
    borderBottomRightRadius: 3,
  },
  main: {
    flex: 1,
    gap: space.xs + 2,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
  },
  brand: {
    ...textVariants.bodyStrong,
    color: t.text,
    flexShrink: 1,
  },
  battery: {
    alignItems: 'flex-end',
  },
  batteryValue: {
    ...textVariants.numeral,
    fontSize: 22,
    lineHeight: 26,
    color: t.text,
  },
  batteryLabel: {
    ...textVariants.caption,
    color: t.textMuted,
  },
}));
