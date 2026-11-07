import { useRouter } from 'expo-router';
import { Pressable, View } from 'react-native';

import { FleetActions } from './FleetActions';
import type { Vehicle } from '@/api/types';
import { AppText } from '@/components/AppText';
import { BatteryRing } from '@/components/BatteryRing';
import { BottomSheet, type SheetFrameProps } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { Icon } from '@/components/Icon';
import { Plate } from '@/components/Plate';
import { VehicleStatusPill } from '@/components/StatusPill';
import { Fact, FactStrip } from '@/features/map/Facts';
import { estimatedRangeKm } from '@/features/map/vehicleInfo';
import { formatDistance } from '@/lib/format';
import { distanceMeters, type LatLng } from '@/lib/geo';
import { makeStyles, space, useTheme } from '@/theme';

type Props = SheetFrameProps & {
  vehicle: Vehicle;
  userLocation: LatLng | null;
  onClose: () => void;
};

/** Filo haritasında seçilen araç: durumu, bataryası ve yapılabilecek işlemler. */
export function FleetVehicleSheet({ vehicle, userLocation, onClose, ...frame }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const router = useRouter();
  const distance = userLocation ? distanceMeters(userLocation, vehicle) : null;

  const header = (
    <View style={styles.header}>
      <BatteryRing percentage={vehicle.batteryPercentage} size={60} />
      <View style={styles.titleBlock}>
        <AppText variant="title" numberOfLines={1}>
          {vehicle.brand}
        </AppText>
        <View style={styles.meta}>
          <Plate id={vehicle.id} kind="Araç" />
          <VehicleStatusPill status={vehicle.status} />
        </View>
      </View>
      <Pressable
        accessibilityRole="button"
        accessibilityLabel="Kapat"
        hitSlop={8}
        onPress={onClose}
        style={({ pressed }) => [styles.close, pressed && styles.closePressed]}>
        <Icon name="close" size={20} color={theme.text} />
      </Pressable>
    </View>
  );

  return (
    <BottomSheet {...frame} mode="dismissible" onDismiss={onClose} header={header} accessibilityLabel="Seçilen araç">
      <FactStrip>
        <Fact icon="speedometer-outline" value={`~${estimatedRangeKm(vehicle)} km`} label="kalan menzil" />
        <Fact icon="flash-outline" value={`${vehicle.rangeKm} km`} label="tam şarj" />
        {distance !== null ? <Fact icon="navigate-outline" value={formatDistance(distance)} label="uzaklık" /> : null}
      </FactStrip>
      <FleetActions vehicle={vehicle} />
      <Button
        title="Ayrıntılar ve QR kodu"
        variant="ghost"
        icon="qr-code-outline"
        onPress={() => router.push({ pathname: '/fleet/vehicle/[id]', params: { id: vehicle.id } })}
      />
    </BottomSheet>
  );
}

const useStyles = makeStyles((t) => ({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md + 2,
  },
  titleBlock: {
    flex: 1,
    gap: space.xs + 2,
  },
  meta: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: space.sm,
  },
  close: {
    width: 36,
    height: 36,
    borderRadius: 18,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.surfaceAlt,
    alignSelf: 'flex-start',
  },
  closePressed: {
    backgroundColor: t.lineStrong,
  },
}));
