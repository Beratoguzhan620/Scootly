import { useMutation } from '@tanstack/react-query';
import { Text, View } from 'react-native';

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
import { useNow } from '@/hooks/useNow';
import { confirm } from '@/lib/confirm';
import { formatDistance, formatDuration } from '@/lib/format';
import { distanceMeters, type LatLng } from '@/lib/geo';
import { haptics } from '@/lib/haptics';
import { useReservation, type StoredReservation } from '@/ride/ReservationProvider';
import { makeStyles, radius, space, textVariants, useTheme } from '@/theme';

type Props = SheetFrameProps & {
  reservation: StoredReservation;
  /** Rezerve edilen aracın güncel bilgisi (yüklenirken null). */
  vehicle: Vehicle | null;
  userLocation: LatLng | null;
};

/** Rezervasyon sürerken: geri sayım, aracın bilgileri, sürüşü başlatma. Aşağı çekince yalnızca sayaç kalır. */
export function ReservationSheet({ reservation, vehicle, userLocation, ...frame }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const { cancel, startRide } = useReservation();
  const now = useNow(1_000);
  const remaining = reservationRemainingMs(reservation.reservedAt, now);
  const minutesLeft = Math.ceil(remaining / 60_000);

  const start = useMutation({
    mutationFn: startRide,
    onSuccess: () => haptics.impact(),
    onError: () => haptics.error(),
  });
  const cancellation = useMutation({ mutationFn: cancel });
  const busy = start.isPending || cancellation.isPending;
  const error = start.error ?? cancellation.error;
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
          {vehicle ? `${vehicle.brand} seni bekliyor` : 'Scooter seni bekliyor'}
        </AppText>
      </View>
      <View
        style={styles.countdown}
        accessible
        accessibilityRole="timer"
        accessibilityLabel={`Rezervasyonun bitmesine ${minutesLeft} dakika`}>
        <Text style={[styles.countdownValue, { color: theme.onBrand }]}>{formatDuration(remaining)}</Text>
        <Text style={[styles.countdownLabel, { color: theme.onBrand }]}>kaldı</Text>
      </View>
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

      {error ? <Notice tone="danger">{errorMessage(error)}</Notice> : null}

      <Button
        title="Sürüşü başlat"
        size="lg"
        icon="play"
        loading={start.isPending}
        disabled={busy}
        onPress={() => start.mutate()}
      />
      <Button
        title="Rezervasyonu iptal et"
        variant="dangerGhost"
        loading={cancellation.isPending}
        disabled={busy}
        onPress={() => void onCancel()}
      />
    </BottomSheet>
  );
}

const useStyles = makeStyles((t) => ({
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
  },
  titleBlock: {
    flex: 1,
    gap: 2,
  },
  countdown: {
    alignItems: 'center',
    backgroundColor: t.brand,
    borderRadius: radius.control,
    paddingHorizontal: space.md + 2,
    paddingTop: space.xs + 2,
    paddingBottom: space.xs,
    minWidth: 92,
  },
  countdownValue: {
    ...textVariants.numeral,
    fontSize: 30,
    lineHeight: 32,
  },
  countdownLabel: {
    ...textVariants.captionStrong,
    marginTop: -2,
  },
  plateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
}));
