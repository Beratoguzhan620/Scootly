import { useMutation, useQueryClient } from '@tanstack/react-query';
import { StyleSheet, View } from 'react-native';

import { Stat } from './Stat';
import { errorMessage, isApiStatus } from '@/api/client';
import { queryKeys } from '@/api/queries';
import { ridesApi, vehiclesApi } from '@/api/scootly';
import type { Ride } from '@/api/types';
import { AlertBox } from '@/components/AlertBox';
import { AppText, Code } from '@/components/AppText';
import { RideStatusBadge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { estimateFare } from '@/domain/fare';
import { STANDARD_TARIFF } from '@/domain/rules';
import { useNow } from '@/hooks/useNow';
import { confirm } from '@/lib/confirm';
import { formatCurrency, formatDuration, formatTime, parseServerDate, shortId } from '@/lib/format';
import type { LatLng } from '@/lib/geo';
import { ensureLocationPermission, getCurrentLocation } from '@/lib/location';
import { shadows, spacing } from '@/theme';

type Props = {
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

export function RideInProgressCard({ ride, onCompleted }: Props) {
  const queryClient = useQueryClient();
  const now = useNow(1_000);
  const startedAt = parseServerDate(ride.startedAt);
  const elapsed = now - startedAt.getTime();

  const completion = useMutation({
    mutationFn: async () => ridesApi.complete(ride.id, await resolveEndLocation(ride.vehicleId)),
    onSuccess: async (result) => {
      queryClient.setQueryData(queryKeys.activeRide, null);
      await queryClient.invalidateQueries({ queryKey: queryKeys.rides });
      await queryClient.invalidateQueries({ queryKey: queryKeys.vehicles });
      onCompleted(result.rideId);
    },
    onError: async (error) => {
      // Sürüş sunucuda zaten bitmiş olabilir (ör. 2 saati aştığı için sistem sonlandırdı).
      if (isApiStatus(error, 404, 409)) await queryClient.invalidateQueries({ queryKey: queryKeys.activeRide });
    },
  });

  const onFinish = async () => {
    const confirmed = await confirm({
      title: 'Sürüşü bitir',
      message: "Scooter'ı uygun bir yere park ettiğinden emin ol. Sürüş bulunduğun konumda sonlanır ve ücret hesaplanır.",
      confirmLabel: 'Bitir',
    });
    if (confirmed) completion.mutate();
  };

  return (
    <Card style={styles.card} bodyStyle={styles.body}>
      <View style={styles.header}>
        <AppText variant="h5">Sürüştesin</AppText>
        <RideStatusBadge status={ride.status} />
      </View>

      <View style={styles.stats}>
        <Stat value={formatDuration(elapsed)} label="süre" />
        <Stat value={formatCurrency(estimateFare(elapsed))} label="tahmini ücret" />
      </View>

      <AppText variant="small" muted>
        Dakikası {formatCurrency(STANDARD_TARIFF.perMinuteRate)} · Başlangıç {formatTime(startedAt)} · Araç{' '}
        <Code>{shortId(ride.vehicleId)}</Code>
      </AppText>

      {completion.isError ? <AlertBox tone="danger">{errorMessage(completion.error)}</AlertBox> : null}

      <Button title="Sürüşü Bitir" size="lg" loading={completion.isPending} onPress={onFinish} />
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
    gap: spacing[3],
  },
});
