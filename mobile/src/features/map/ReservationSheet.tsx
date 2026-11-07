import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { View } from 'react-native';

import { Fact, FactStrip } from './Facts';
import { estimatedRangeKm } from './vehicleInfo';
import { errorMessage } from '@/api/client';
import type { Vehicle } from '@/api/types';
import { AppText } from '@/components/AppText';
import { BottomSheet, type SheetFrameProps } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { Plate } from '@/components/Plate';
import { ProgressBar } from '@/components/ProgressBar';
import { batteryPercent } from '@/domain/battery';
import { reservationRemainingMs } from '@/domain/reservation';
import { RESERVATION_DURATION_MS } from '@/domain/rules';
import { CountdownPlate } from '@/features/reservations/CountdownPlate';
import { useNow } from '@/hooks/useNow';
import { confirm } from '@/lib/confirm';
import { formatDistance } from '@/lib/format';
import { distanceMeters, type LatLng } from '@/lib/geo';
import { useReservation, type StoredReservation } from '@/ride/ReservationProvider';
import { makeStyles, space } from '@/theme';

type Props = SheetFrameProps & {
  reservation: StoredReservation;
  /** Rezerve edilen aracın güncel bilgisi (yüklenirken null). */
  vehicle: Vehicle | null;
  userLocation: LatLng | null;
};

/**
 * Rezervasyon sürerken: geri sayım, aracın bilgileri ve sürüşü başlatma. Sürüş scooter'ın QR kodu okutularak
 * başlar (sürücünün aracın yanında olduğu böyle anlaşılır). Aşağı çekince yalnızca sayaç kalır.
 */
export function ReservationSheet({ reservation, vehicle, userLocation, ...frame }: Props) {
  const styles = useStyles();
  const router = useRouter();
  const { cancel } = useReservation();
  const now = useNow(1_000);
  const remaining = reservationRemainingMs(reservation.reservedAt, now);

  const cancellation = useMutation({ mutationFn: cancel });
  const distance = vehicle && userLocation ? distanceMeters(userLocation, vehicle) : null;

  const onCancel = async () => {
    const confirmed = await confirm({
      title: 'Rezervasyonu iptal et',
      message: 'Scooter yeniden müsait olur ve başka sürücüler rezerve edebilir.',
      confirmLabel: 'İptal et',
      destructive: true,
    });
    if (confirmed) cancellation.mutate();
  };

  const header = (
    <View style={styles.header}>
      <View style={styles.titleBlock}>
        <AppText variant="title">Rezervasyonun</AppText>
        <AppText muted numberOfLines={1}>
          {`${vehicle?.brand ?? reservation.brand ?? 'Scooter'} seni bekliyor`}
        </AppText>
      </View>
      <CountdownPlate remaining={remaining} />
    </View>
  );

  return (
    <BottomSheet {...frame} mode="collapsible" header={header} accessibilityLabel="Rezervasyon">
      <ProgressBar value={remaining / RESERVATION_DURATION_MS} accessibilityLabel="Rezervasyon süresi" />

      <FactStrip>
        <Fact
          icon="battery-half-outline"
          value={vehicle ? `%${batteryPercent(vehicle.batteryPercentage)}` : '—'}
          label="batarya"
        />
        <Fact icon="speedometer-outline" value={vehicle ? `~${estimatedRangeKm(vehicle)} km` : '—'} label="menzil" />
        {distance !== null ? <Fact icon="navigate-outline" value={formatDistance(distance)} label="uzaklık" /> : null}
      </FactStrip>

      <View style={styles.plateRow}>
        <AppText variant="caption" muted>
          Scooter&apos;daki kod
        </AppText>
        <Plate id={reservation.vehicleId} kind="Araç" />
      </View>

      {cancellation.isError ? <Notice tone="danger">{errorMessage(cancellation.error)}</Notice> : null}

      <Button
        title="QR okut ve başlat"
        size="lg"
        icon="qr-code"
        disabled={cancellation.isPending}
        accessibilityHint="Kamerayı açar; scooter'ın üstündeki QR kodu okutunca ödeme ve başlatma ekranı gelir."
        onPress={() => router.push('/scan')}
      />
      <Button
        title="Rezervasyonu iptal et"
        variant="dangerGhost"
        loading={cancellation.isPending}
        onPress={() => void onCancel()}
      />
    </BottomSheet>
  );
}

const useStyles = makeStyles(() => ({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
  },
  titleBlock: {
    flex: 1,
    gap: 2,
  },
  plateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
}));
