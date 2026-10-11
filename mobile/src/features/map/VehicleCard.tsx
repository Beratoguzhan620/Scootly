import { useMutation } from '@tanstack/react-query';
import { Pressable, StyleSheet, Text, View } from 'react-native';

import { estimatedRangeKm } from './vehicleInfo';
import { errorMessage } from '@/api/client';
import type { Vehicle } from '@/api/types';
import { AlertBox } from '@/components/AlertBox';
import { AppText, Code } from '@/components/AppText';
import { VehicleStatusBadge } from '@/components/Badge';
import { BatteryIndicator } from '@/components/BatteryIndicator';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { MIN_RENTABLE_BATTERY } from '@/domain/rules';
import { formatDistance, shortId } from '@/lib/format';
import { distanceMeters, type LatLng } from '@/lib/geo';
import { useReservation } from '@/ride/ReservationProvider';
import { colors, shadows, spacing, typography } from '@/theme';

type Props = {
  vehicle: Vehicle;
  userLocation: LatLng | null;
  onClose: () => void;
};

/** Haritada seçilen müsait araç. */
export function VehicleCard({ vehicle, userLocation, onClose }: Props) {
  const { reserve } = useReservation();
  const reservation = useMutation({ mutationFn: () => reserve(vehicle.id) });
  const rentable = vehicle.batteryPercentage >= MIN_RENTABLE_BATTERY;
  const distance = userLocation ? distanceMeters(userLocation, vehicle) : null;

  return (
    <Card style={styles.card} bodyStyle={styles.body}>
      <View style={styles.header}>
        <View style={styles.titleBlock}>
          <AppText variant="h5">{vehicle.brand}</AppText>
          <VehicleStatusBadge status={vehicle.status} />
        </View>
        <Pressable accessibilityRole="button" accessibilityLabel="Kapat" hitSlop={12} onPress={onClose}>
          <Text style={styles.close}>×</Text>
        </Pressable>
      </View>

      <View style={styles.facts}>
        <BatteryIndicator percentage={vehicle.batteryPercentage} />
        <AppText>Menzil ~{estimatedRangeKm(vehicle)} km</AppText>
        {distance !== null ? <AppText muted>{formatDistance(distance)} uzakta</AppText> : null}
      </View>

      <AppText variant="small" muted>
        Araç kimliği <Code>{shortId(vehicle.id)}</Code>
      </AppText>

      {!rentable ? (
        <AlertBox tone="warning">
          {`Bu aracın bataryası kiralama için çok düşük (en az %${MIN_RENTABLE_BATTERY}).`}
        </AlertBox>
      ) : null}

      {reservation.isError ? <AlertBox tone="danger">{errorMessage(reservation.error)}</AlertBox> : null}

      <Button
        title="Rezerve Et"
        size="lg"
        disabled={!rentable}
        loading={reservation.isPending}
        onPress={() => reservation.mutate()}
      />
      <AppText variant="small" muted align="center">
        Araç 10 dakika boyunca sana ayrılır.
      </AppText>
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
    alignItems: 'flex-start',
    justifyContent: 'space-between',
  },
  titleBlock: {
    flex: 1,
    gap: spacing[1],
  },
  close: {
    ...typography.h4,
    color: colors.muted,
    lineHeight: 24,
  },
  facts: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    alignItems: 'center',
    columnGap: spacing[3],
    rowGap: spacing[1],
  },
});
