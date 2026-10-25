import { useMutation, useQueryClient } from '@tanstack/react-query';
import { Text, View } from 'react-native';

import { errorMessage, isApiStatus } from '@/api/client';
import { queryKeys } from '@/api/queries';
import { ridesApi, vehiclesApi } from '@/api/scootly';
import type { Ride } from '@/api/types';
import { BottomSheet, type SheetFrameProps } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { Plate } from '@/components/Plate';
import { estimateFare } from '@/domain/fare';
import { STANDARD_TARIFF } from '@/domain/rules';
import { useNow } from '@/hooks/useNow';
import { confirm } from '@/lib/confirm';
import { formatCurrency, formatDuration, formatTime, parseServerDate } from '@/lib/format';
import type { LatLng } from '@/lib/geo';
import { haptics } from '@/lib/haptics';
import { ensureLocationPermission, getCurrentLocation } from '@/lib/location';
import { makeStyles, radius, space, textVariants } from '@/theme';

type Props = SheetFrameProps & {
  ride: Ride;
  onCompleted: (rideId: string) => void;
};

/**
 * Sürüşün bitiş konumu: önce telefonun konumu, alınamazsa aracın son bildirdiği konum.
 * Sunucu, aracın taze telemetrisi varsa park konumu olarak zaten onu kullanıyor.
 */
async function resolveEndLocation(vehicleId: string): Promise<LatLng> {
  if (await ensureLocationPermission(true)) {
    const current = await getCurrentLocation();
    if (current) return current;
  }
  const vehicle = await vehiclesApi.get(vehicleId);
  return { latitude: vehicle.latitude, longitude: vehicle.longitude };
}

/** Sürüş sırasında: ters renkli (lacivert) panel, sarı sayaç ve canlı ücret tahmini. */
export function RideSheet({ ride, onCompleted, ...frame }: Props) {
  const styles = useStyles();
  const queryClient = useQueryClient();
  const now = useNow(1_000);
  const startedAt = parseServerDate(ride.startedAt);
  const elapsed = now - startedAt.getTime();
  const elapsedMinutes = Math.floor(Math.max(0, elapsed) / 60_000);

  const completion = useMutation({
    mutationFn: async () => ridesApi.complete(ride.id, await resolveEndLocation(ride.vehicleId)),
    onSuccess: async (result) => {
      haptics.success();
      queryClient.setQueryData(queryKeys.activeRide, null);
      await queryClient.invalidateQueries({ queryKey: queryKeys.rides });
      await queryClient.invalidateQueries({ queryKey: queryKeys.vehicles });
      onCompleted(result.rideId);
    },
    onError: async (error) => {
      haptics.error();
      // Sürüş sunucuda zaten bitmiş olabilir (ör. 2 saati aştığı için sistem sonlandırdı).
      if (isApiStatus(error, 404, 409)) await queryClient.invalidateQueries({ queryKey: queryKeys.activeRide });
    },
  });

  const onFinish = async () => {
    const confirmed = await confirm({
      title: 'Sürüşü bitir',
      message: "Scooter'ı uygun bir yere park ettiğinden emin ol. Sürüş bulunduğun konumda biter ve ücret hesaplanır.",
      confirmLabel: 'Bitir',
    });
    if (confirmed) completion.mutate();
  };

  const header = (
    <View style={styles.header}>
      <View style={styles.titleBlock}>
        <Text style={styles.title} accessibilityRole="header">
          Sürüştesin
        </Text>
        <Text style={styles.muted}>{`Başlangıç ${formatTime(startedAt)}`}</Text>
      </View>
      <View
        style={styles.timer}
        accessible
        accessibilityRole="timer"
        accessibilityLabel={`Sürüş süresi ${elapsedMinutes} dakika`}>
        <Text style={styles.timerValue}>{formatDuration(elapsed)}</Text>
      </View>
    </View>
  );

  return (
    <BottomSheet {...frame} mode="collapsible" variant="ride" header={header} accessibilityLabel="Sürüş">
      <View style={styles.fareRow}>
        <View accessible accessibilityLabel={`Tahmini ücret ${formatCurrency(estimateFare(elapsed))}`}>
          <Text style={styles.fare}>{formatCurrency(estimateFare(elapsed))}</Text>
          <Text style={styles.muted}>tahmini ücret</Text>
        </View>
        <View style={styles.tariff}>
          <Text style={styles.tariffValue}>{formatCurrency(STANDARD_TARIFF.perMinuteRate)}</Text>
          <Text style={styles.muted}>dakikası</Text>
        </View>
      </View>

      <View style={styles.plateRow}>
        <Text style={styles.muted}>Scooter</Text>
        <Plate id={ride.vehicleId} kind="Araç" inverse />
      </View>

      {completion.isError ? <Notice tone="danger">{errorMessage(completion.error)}</Notice> : null}

      <Button
        title="Sürüşü bitir"
        size="lg"
        icon="flag"
        loading={completion.isPending}
        onPress={() => void onFinish()}
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
  title: {
    ...textVariants.title,
    color: t.ride.text,
  },
  muted: {
    ...textVariants.caption,
    fontSize: 14,
    color: t.ride.muted,
  },
  timer: {
    borderRadius: radius.control,
    borderWidth: 2,
    borderColor: t.ride.accent,
    paddingHorizontal: space.md,
    paddingVertical: space.xs,
  },
  timerValue: {
    ...textVariants.numeral,
    fontSize: 30,
    lineHeight: 34,
    color: t.ride.accent,
  },
  fareRow: {
    flexDirection: 'row',
    alignItems: 'flex-end',
    justifyContent: 'space-between',
    gap: space.lg,
  },
  fare: {
    ...textVariants.display,
    color: t.ride.text,
  },
  tariff: {
    alignItems: 'flex-end',
    paddingBottom: 2,
  },
  tariffValue: {
    ...textVariants.bodyStrong,
    fontSize: 18,
    color: t.ride.text,
  },
  plateRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    borderTopWidth: 1,
    borderTopColor: t.ride.line,
    paddingTop: space.md,
  },
}));
