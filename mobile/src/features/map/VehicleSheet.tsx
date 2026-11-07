import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { Pressable, View } from 'react-native';

import { Fact, FactStrip } from './Facts';
import { estimatedRangeKm } from './vehicleInfo';
import { errorMessage } from '@/api/client';
import type { Vehicle } from '@/api/types';
import { AppText } from '@/components/AppText';
import { BatteryRing } from '@/components/BatteryRing';
import { BottomSheet, type SheetFrameProps } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { Icon } from '@/components/Icon';
import { Notice } from '@/components/Notice';
import { Plate } from '@/components/Plate';
import { MIN_RENTABLE_BATTERY, RESERVATION_DURATION_MS } from '@/domain/rules';
import { formatDistance, formatWalk } from '@/lib/format';
import { distanceMeters, type LatLng } from '@/lib/geo';
import { haptics } from '@/lib/haptics';
import { useReservation } from '@/ride/ReservationProvider';
import { makeStyles, space, useTheme } from '@/theme';

type Props = SheetFrameProps & {
  vehicle: Vehicle;
  userLocation: LatLng | null;
  onClose: () => void;
};

/** Haritada seçilen müsait scooter. Aşağı çekince kapanır. */
export function VehicleSheet({ vehicle, userLocation, onClose, ...frame }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const router = useRouter();
  const { reserve } = useReservation();
  const reservation = useMutation({
    mutationFn: () => reserve(vehicle),
    onSuccess: () => haptics.success(),
    onError: () => haptics.error(),
  });
  const rentable = vehicle.batteryPercentage >= MIN_RENTABLE_BATTERY;
  const distance = userLocation ? distanceMeters(userLocation, vehicle) : null;

  const header = (
    <View style={styles.header}>
      <BatteryRing percentage={vehicle.batteryPercentage} size={60} />
      <View style={styles.titleBlock}>
        <AppText variant="title" numberOfLines={1}>
          {vehicle.brand}
        </AppText>
        <Plate id={vehicle.id} kind="Araç" />
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
    <BottomSheet {...frame} mode="dismissible" onDismiss={onClose} header={header} accessibilityLabel="Seçilen scooter">
      <FactStrip>
        <Fact icon="speedometer-outline" value={`~${estimatedRangeKm(vehicle)} km`} label="menzil" />
        {distance !== null ? <Fact icon="navigate-outline" value={formatDistance(distance)} label="uzaklık" /> : null}
        {distance !== null ? <Fact icon="walk-outline" value={formatWalk(distance)} label="yürüyüş" /> : null}
      </FactStrip>

      {!rentable ? (
        <Notice tone="warning">
          {`Bu scooter'ın bataryası kiralamaya yetmiyor (en az %${MIN_RENTABLE_BATTERY}). Yakındaki başka birini seç.`}
        </Notice>
      ) : null}
      {reservation.isError ? <Notice tone="danger">{errorMessage(reservation.error)}</Notice> : null}

      <Button
        title="Rezerve et"
        size="lg"
        icon="timer-outline"
        disabled={!rentable}
        loading={reservation.isPending}
        onPress={() => reservation.mutate()}
      />
      <AppText variant="caption" muted align="center">
        {`Scooter ${RESERVATION_DURATION_MS / 60_000} dakika boyunca sana ayrılır.`}
      </AppText>
      {rentable ? (
        <Button
          title="Yanındayım, QR okut"
          variant="ghost"
          icon="qr-code-outline"
          disabled={reservation.isPending}
          onPress={() => router.push('/scan')}
        />
      ) : null}
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
