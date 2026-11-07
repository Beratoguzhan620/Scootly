import { useLocalSearchParams, useRouter } from 'expo-router';
import { Text, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { useFleetVehicle } from '@/api/queries';
import { AppText } from '@/components/AppText';
import { BatteryRing } from '@/components/BatteryRing';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { Plate } from '@/components/Plate';
import { QrCode } from '@/components/QrCode';
import { InfoRow, RowGroup } from '@/components/Rows';
import { Screen } from '@/components/Screen';
import { LoadingView } from '@/components/StateViews';
import { VehicleStatusPill } from '@/components/StatusPill';
import { Surface } from '@/components/Surface';
import { batteryPercent } from '@/domain/battery';
import { vehicleQrPayload } from '@/domain/vehicleCode';
import { FleetActions } from '@/features/fleet/FleetActions';
import { estimatedRangeKm } from '@/features/map/vehicleInfo';
import { shortId } from '@/lib/format';
import { makeStyles, space, textVariants } from '@/theme';

/** Aracın sayfası: durum işlemleri ve scooter'a yapıştırılacak QR kodu. */
export default function FleetVehicleScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const styles = useStyles();
  const router = useRouter();
  const query = useFleetVehicle(id);
  const vehicle = query.data;

  if (query.isPending) return <LoadingView message="Araç yükleniyor…" />;

  if (query.isError || !vehicle) {
    return (
      <Screen edges={['bottom']}>
        <Notice tone="danger" action={{ label: 'Tekrar dene', onPress: () => void query.refetch() }}>
          {errorMessage(query.error, 'Araç bulunamadı.')}
        </Notice>
      </Screen>
    );
  }

  const code = shortId(vehicle.id).toUpperCase();

  return (
    <Screen edges={['bottom']}>
      <View style={styles.hero}>
        <BatteryRing percentage={vehicle.batteryPercentage} size={64} />
        <View style={styles.heroText}>
          <AppText variant="title" numberOfLines={1}>
            {vehicle.brand}
          </AppText>
          <View style={styles.meta}>
            <Plate id={vehicle.id} kind="Araç" />
            <VehicleStatusPill status={vehicle.status} />
          </View>
        </View>
      </View>

      <FleetActions vehicle={vehicle} />

      <Surface style={styles.qrCard}>
        <View style={styles.qrFrame}>
          <QrCode value={vehicleQrPayload(vehicle.id)} size={232} accessibilityLabel={`${code} kodlu scooter'ın QR kodu`} />
        </View>
        <Text style={styles.code} accessibilityLabel={`Plaka kodu ${code.split('').join(' ')}`}>
          {code}
        </Text>
        <AppText variant="caption" muted align="center" style={styles.qrNote}>
          Bu kodu scooter&apos;ın gidonuna yapıştır. Sürücüler okutarak sürüşe başlar; okunmazsa altındaki kodu yazar.
        </AppText>
      </Surface>

      <RowGroup>
        <InfoRow label="Batarya" value={`%${batteryPercent(vehicle.batteryPercentage)}`} />
        <InfoRow label="Kalan menzil" value={`~${estimatedRangeKm(vehicle)} km`} />
        <InfoRow label="Tam şarj menzili" value={`${vehicle.rangeKm} km`} />
        <InfoRow label="Konum" value={`${vehicle.latitude.toFixed(5)}, ${vehicle.longitude.toFixed(5)}`} />
      </RowGroup>

      <View style={styles.idBlock}>
        <AppText variant="caption" muted>
          Araç kimliği
        </AppText>
        <AppText variant="caption" selectable style={styles.id}>
          {vehicle.id}
        </AppText>
      </View>

      <Button
        title="Haritada göster"
        variant="secondary"
        icon="map-outline"
        onPress={() =>
          router.dismissTo({
            pathname: '/fleet',
            params: { focus: vehicle.id, lat: String(vehicle.latitude), lng: String(vehicle.longitude) },
          })
        }
      />
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  hero: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.lg,
    paddingTop: space.sm,
  },
  heroText: {
    flex: 1,
    gap: space.xs + 2,
  },
  meta: {
    flexDirection: 'row',
    alignItems: 'center',
    flexWrap: 'wrap',
    gap: space.sm,
  },
  qrCard: {
    alignItems: 'center',
    gap: space.sm,
  },
  qrFrame: {
    padding: space.xs,
    borderRadius: 16,
    backgroundColor: '#FFFFFF',
  },
  code: {
    ...textVariants.numeral,
    fontSize: 34,
    lineHeight: 38,
    letterSpacing: 4,
    color: t.text,
  },
  qrNote: {
    maxWidth: 300,
  },
  idBlock: {
    gap: 2,
    paddingHorizontal: space.xs,
  },
  id: {
    color: t.textMuted,
    fontVariant: ['tabular-nums'],
  },
}));
