import { useMutation } from '@tanstack/react-query';
import { useRouter } from 'expo-router';
import { View } from 'react-native';

import { CountdownPlate } from './CountdownPlate';
import { errorMessage } from '@/api/client';
import { useVehicle } from '@/api/queries';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { Plate } from '@/components/Plate';
import { ProgressBar } from '@/components/ProgressBar';
import { Surface } from '@/components/Surface';
import { batteryPercent } from '@/domain/battery';
import { reservationRemainingMs } from '@/domain/reservation';
import { RESERVATION_DURATION_MS } from '@/domain/rules';
import { Fact, FactStrip } from '@/features/map/Facts';
import { estimatedRangeKm } from '@/features/map/vehicleInfo';
import { useNow } from '@/hooks/useNow';
import { confirm } from '@/lib/confirm';
import { formatDistance, formatTime } from '@/lib/format';
import { distanceMeters, type LatLng } from '@/lib/geo';
import { useReservation, type StoredReservation } from '@/ride/ReservationProvider';
import { makeStyles, space } from '@/theme';

type Props = {
  reservation: StoredReservation;
  userLocation: LatLng | null;
};

/** Rezervasyonlar sayfasının başındaki kart: geri sayım, scooter'ın bilgileri ve yapılacaklar. */
export function ActiveReservationCard({ reservation, userLocation }: Props) {
  const styles = useStyles();
  const router = useRouter();
  const { cancel, serverBacked } = useReservation();
  const vehicle = useVehicle(reservation.vehicleId).data ?? null;
  const now = useNow(1_000);
  const remaining = reservationRemainingMs(reservation.reservedAt, now);
  const cancellation = useMutation({ mutationFn: cancel });
  const distance = vehicle && userLocation ? distanceMeters(userLocation, vehicle) : null;
  const expiresAt = new Date(reservation.reservedAt + RESERVATION_DURATION_MS);

  const onCancel = async () => {
    const confirmed = await confirm({
      title: 'Rezervasyonu iptal et',
      message: 'Scooter yeniden müsait olur ve başka sürücüler rezerve edebilir.',
      confirmLabel: 'İptal et',
      destructive: true,
    });
    if (confirmed) cancellation.mutate();
  };

  return (
    <Surface style={styles.card}>
      <View style={styles.header}>
        <View style={styles.titleBlock}>
          <AppText variant="title" numberOfLines={1}>
            {vehicle?.brand ?? reservation.brand ?? 'Scooter'}
          </AppText>
          <View style={styles.meta}>
            <Plate id={reservation.vehicleId} kind="Araç" />
            <AppText variant="caption" muted>
              {`Bitiş ${formatTime(expiresAt)}`}
            </AppText>
          </View>
        </View>
        <CountdownPlate remaining={remaining} />
      </View>

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

      {cancellation.isError ? <Notice tone="danger">{errorMessage(cancellation.error)}</Notice> : null}

      <Button
        title="QR okut ve başlat"
        size="lg"
        icon="qr-code"
        disabled={cancellation.isPending}
        onPress={() => router.push('/scan')}
      />
      <View style={styles.row}>
        <Button
          title="Haritada göster"
          variant="secondary"
          icon="map-outline"
          style={styles.half}
          onPress={() => router.navigate({ pathname: '/', params: { focus: reservation.vehicleId } })}
        />
        <Button
          title="İptal et"
          variant="dangerGhost"
          style={styles.half}
          loading={cancellation.isPending}
          onPress={() => void onCancel()}
        />
      </View>

      <AppText variant="caption" muted align="center">
        {serverBacked === false
          ? 'Bu sunucu rezervasyon sorgulamayı desteklemiyor; rezervasyonun yalnızca bu telefonda görünür.'
          : 'Rezervasyonun hesabına kayıtlı; uygulamayı kapatıp açsan da burada görünür.'}
      </AppText>
    </Surface>
  );
}

const useStyles = makeStyles(() => ({
  card: {
    gap: space.lg,
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
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
  row: {
    flexDirection: 'row',
    gap: space.sm,
  },
  half: {
    flex: 1,
    paddingHorizontal: space.md,
  },
}));
