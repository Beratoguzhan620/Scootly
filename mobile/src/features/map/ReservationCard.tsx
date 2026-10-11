import { useMutation } from '@tanstack/react-query';
import { StyleSheet, View } from 'react-native';

import { Stat } from './Stat';
import { estimatedRangeKm } from './vehicleInfo';
import { errorMessage } from '@/api/client';
import type { Vehicle } from '@/api/types';
import { AlertBox } from '@/components/AlertBox';
import { AppText, Code } from '@/components/AppText';
import { VehicleStatusBadge } from '@/components/Badge';
import { BatteryIndicator } from '@/components/BatteryIndicator';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { ProgressBar } from '@/components/ProgressBar';
import { reservationRemainingMs } from '@/domain/reservation';
import { RESERVATION_DURATION_MS } from '@/domain/rules';
import { useNow } from '@/hooks/useNow';
import { confirm } from '@/lib/confirm';
import { formatDistance, formatDuration, shortId } from '@/lib/format';
import { distanceMeters, type LatLng } from '@/lib/geo';
import { useReservation, type StoredReservation } from '@/ride/ReservationProvider';
import { colors, shadows, spacing } from '@/theme';

type Props = {
  reservation: StoredReservation;
  /** Rezerve edilen aracın güncel bilgisi (yüklenirken null). */
  vehicle: Vehicle | null;
  userLocation: LatLng | null;
};

export function ReservationCard({ reservation, vehicle, userLocation }: Props) {
  const { cancel, startRide } = useReservation();
  const now = useNow(1_000);
  const remaining = reservationRemainingMs(reservation.reservedAt, now);

  const start = useMutation({ mutationFn: startRide });
  const cancellation = useMutation({ mutationFn: cancel });
  const busy = start.isPending || cancellation.isPending;
  const error = start.error ?? cancellation.error;
  const distance = vehicle && userLocation ? distanceMeters(userLocation, vehicle) : null;

  const onCancel = async () => {
    const confirmed = await confirm({
      title: 'Rezervasyonu iptal et',
      message: 'Araç yeniden müsait olur ve başka sürücüler rezerve edebilir.',
      confirmLabel: 'İptal Et',
      destructive: true,
    });
    if (confirmed) cancellation.mutate();
  };

  return (
    <Card style={styles.card} bodyStyle={styles.body}>
      <View style={styles.header}>
        <AppText variant="h5">Rezervasyonun</AppText>
        <VehicleStatusBadge status="Reserved" />
      </View>

      <View style={styles.stats}>
        <Stat
          value={formatDuration(remaining)}
          label="kalan süre (yaklaşık)"
          accessibilityLabel={`Rezervasyonun bitmesine ${Math.ceil(remaining / 60_000)} dakika`}
        />
        {vehicle ? (
          <View style={styles.vehicleStat}>
            <BatteryIndicator percentage={vehicle.batteryPercentage} />
            <AppText variant="small" muted>
              ~{estimatedRangeKm(vehicle)} km menzil
            </AppText>
          </View>
        ) : null}
      </View>

      <ProgressBar
        value={remaining / RESERVATION_DURATION_MS}
        color={colors.warning}
        accessibilityLabel="Rezervasyon süresi"
      />

      <AppText>
        {vehicle ? `${vehicle.brand} · ` : ''}
        <Code>{shortId(reservation.vehicleId)}</Code>
        {distance !== null ? ` · ${formatDistance(distance)} uzakta` : ''}
      </AppText>

      {error ? <AlertBox tone="danger">{errorMessage(error)}</AlertBox> : null}

      <Button
        title="Sürüşü Başlat"
        size="lg"
        loading={start.isPending}
        disabled={busy}
        onPress={() => start.mutate()}
      />
      <Button
        title="Rezervasyonu İptal Et"
        variant="outline-danger"
        loading={cancellation.isPending}
        disabled={busy}
        onPress={onCancel}
      />
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    boxShadow: shadows.md,
  },
  body: {
    gap: spacing[2],
  },
  header: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  stats: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing[3],
  },
  vehicleStat: {
    alignItems: 'flex-end',
    gap: 2,
  },
});
