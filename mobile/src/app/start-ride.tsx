import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect } from 'react';
import { Pressable, ScrollView, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { errorMessage } from '@/api/client';
import { useActiveRide } from '@/api/queries';
import { AppText } from '@/components/AppText';
import { BatteryRing } from '@/components/BatteryRing';
import { Button } from '@/components/Button';
import { CardBadge } from '@/components/CardBadge';
import { Notice } from '@/components/Notice';
import { Plate } from '@/components/Plate';
import { InfoRow, RowGroup } from '@/components/Rows';
import { LoadingView } from '@/components/StateViews';
import { StatusPill } from '@/components/StatusPill';
import { Surface } from '@/components/Surface';
import { cardLabel, formatCardExpiry } from '@/domain/card';
import { estimateFare } from '@/domain/fare';
import { reservationRemainingMs } from '@/domain/reservation';
import { MIN_RENTABLE_BATTERY, STANDARD_TARIFF } from '@/domain/rules';
import type { ScannedVehicle } from '@/domain/vehicleCode';
import { Fact, FactStrip } from '@/features/map/Facts';
import { estimatedRangeKm } from '@/features/map/vehicleInfo';
import { resolveVehicle } from '@/features/start/resolveVehicle';
import { useNow } from '@/hooks/useNow';
import { useUserLocation } from '@/hooks/useUserLocation';
import { formatCurrency, formatDistance, formatDuration, formatWalk } from '@/lib/format';
import { distanceMeters } from '@/lib/geo';
import { haptics } from '@/lib/haptics';
import { usePayment } from '@/payments/PaymentProvider';
import { useReservation } from '@/ride/ReservationProvider';
import { makeStyles, screenPadding, space, useTheme } from '@/theme';

/** Örnek ücret için gösterilen sürüş süresi */
const EXAMPLE_MINUTES = 10;

/**
 * QR okutulduktan sonra, sürüş başlamadan: scooter, tarife ve ödeme yöntemi. Sürücü kartını onaylayınca kilit açılır.
 * Araç sürücüye ayrılmamışsa (QR ile doğrudan başlatma) önce rezerve edilir.
 */
export default function StartRideScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const queryClient = useQueryClient();
  const params = useLocalSearchParams<{ vehicleId?: string; code?: string }>();
  const target: ScannedVehicle | null = params.vehicleId
    ? { kind: 'id', id: params.vehicleId }
    : params.code
      ? { kind: 'code', code: params.code }
      : null;

  const { reservation, cancel, startRide, refresh } = useReservation();
  const { card, loading: cardLoading } = usePayment();
  const activeRide = useActiveRide().data ?? null;
  const { location } = useUserLocation();
  const now = useNow(1_000);

  const resolved = useQuery({
    // Plaka koduyla aranırken konum gelince çevredeki araçlara da bakılır.
    queryKey: ['start-ride', target, target?.kind === 'code' && location !== null],
    queryFn: ({ signal }) =>
      resolveVehicle(target as ScannedVehicle, {
        queryClient,
        reservedVehicleId: reservation?.vehicleId ?? null,
        location,
        signal,
      }),
    enabled: target !== null,
    retry: false,
    staleTime: 0,
  });
  const vehicle = resolved.data ?? null;

  const mine = vehicle !== null && reservation?.vehicleId === vehicle.id;
  const otherReservation = vehicle !== null && reservation !== null && !mine;
  const lowBattery = vehicle !== null && vehicle.batteryPercentage < MIN_RENTABLE_BATTERY;

  // Rezervasyon başka bir telefondan yapılmışsa önce sunucuyla eşitle.
  useEffect(() => {
    if (vehicle?.status === 'Reserved' && reservation?.vehicleId !== vehicle.id) void refresh();
  }, [vehicle, reservation, refresh]);

  const start = useMutation({
    mutationFn: () => {
      if (!vehicle || !card) throw new Error('Scooter ya da ödeme kartı eksik.');
      return startRide(vehicle, { card: cardLabel(card) });
    },
    onSuccess: () => {
      haptics.impact();
      router.dismissTo('/');
    },
    onError: () => haptics.error(),
  });
  const cancellation = useMutation({ mutationFn: cancel });

  if (!target) {
    return (
      <View style={styles.center}>
        <Notice tone="danger" action={{ label: 'QR okut', onPress: () => router.replace('/scan') }}>
          Scooter seçilmedi. Scooter&apos;ın QR kodunu okut ya da kodunu gir.
        </Notice>
      </View>
    );
  }

  if (resolved.isPending) return <LoadingView message="Scooter bulunuyor…" />;

  if (resolved.isError || !vehicle) {
    return (
      <View style={styles.center}>
        <Notice
          tone="danger"
          title="Bu scooter ile başlanamıyor"
          action={{ label: 'Başka bir kod okut', onPress: () => router.replace('/scan') }}>
          {errorMessage(resolved.error)}
        </Notice>
        <Button title="Tekrar dene" variant="secondary" icon="refresh" onPress={() => void resolved.refetch()} />
      </View>
    );
  }

  const distance = location ? distanceMeters(location, vehicle) : null;
  const blocked = Boolean(activeRide) || otherReservation || lowBattery;
  const ready = !blocked && card !== null;

  return (
    <View style={styles.root}>
      <ScrollView contentContainerStyle={styles.content} keyboardShouldPersistTaps="handled">
        <Surface style={styles.vehicle}>
          <View style={styles.vehicleHeader}>
            <BatteryRing percentage={vehicle.batteryPercentage} size={60} />
            <View style={styles.vehicleTitle}>
              <AppText variant="title" numberOfLines={1}>
                {vehicle.brand}
              </AppText>
              <Plate id={vehicle.id} kind="Araç" />
            </View>
          </View>
          {mine && reservation ? (
            <StatusPill
              label={`Senin için ayrıldı · ${formatDuration(reservationRemainingMs(reservation.reservedAt, now))} kaldı`}
              tone="primary"
            />
          ) : vehicle.status === 'Available' ? (
            <StatusPill label="Müsait" tone="success" />
          ) : null}
          <FactStrip>
            <Fact icon="speedometer-outline" value={`~${estimatedRangeKm(vehicle)} km`} label="menzil" />
            {distance !== null ? (
              <Fact icon="navigate-outline" value={formatDistance(distance)} label="uzaklık" />
            ) : null}
            {distance !== null ? <Fact icon="walk-outline" value={formatWalk(distance)} label="yürüyüş" /> : null}
          </FactStrip>
        </Surface>

        {activeRide ? (
          <Notice tone="brand" action={{ label: 'Haritaya dön', onPress: () => router.dismissTo('/') }}>
            Zaten sürüştesin. Yeni bir sürüşe başlamadan önce devam eden sürüşü bitir.
          </Notice>
        ) : null}
        {otherReservation && reservation ? (
          <Notice
            tone="warning"
            title="Başka bir scooter'ı rezerve ettin"
            action={{
              label: cancellation.isPending ? 'İptal ediliyor…' : 'Rezervasyonu iptal et',
              onPress: () => {
                if (!cancellation.isPending) cancellation.mutate();
              },
            }}>
            {`Rezervasyonun ${reservation.vehicleId.slice(0, 8).toUpperCase()} kodlu scooter için. Bu scooter'la başlamak için önce onu iptal et.`}
          </Notice>
        ) : null}
        {lowBattery ? (
          <Notice tone="warning">
            {`Bu scooter'ın bataryası kiralamaya yetmiyor (en az %${MIN_RENTABLE_BATTERY}). Yakındaki başka birini seç.`}
          </Notice>
        ) : null}

        <View style={styles.section}>
          <AppText variant="heading" accessibilityRole="header">
            Ücret
          </AppText>
          <RowGroup>
            <InfoRow
              label="Kilit açma"
              value={STANDARD_TARIFF.unlockFee > 0 ? formatCurrency(STANDARD_TARIFF.unlockFee) : 'Ücretsiz'}
            />
            <InfoRow label="Dakika ücreti" value={formatCurrency(STANDARD_TARIFF.perMinuteRate)} />
            <InfoRow
              label={`Örnek: ${EXAMPLE_MINUTES} dakika`}
              value={formatCurrency(estimateFare(EXAMPLE_MINUTES * 60_000))}
            />
          </RowGroup>
          <AppText variant="caption" muted style={styles.note}>
            Başlamış her dakika ücretlendirilir (en az 1 dakika). Ücret sürüş bitince kartından çekilir.
          </AppText>
        </View>

        <View style={styles.section}>
          <AppText variant="heading" accessibilityRole="header">
            Ödeme yöntemi
          </AppText>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={card ? `${cardLabel(card)}, değiştir` : 'Kart ekle'}
            onPress={() => router.push('/payment-method')}
            style={({ pressed }) => [styles.payment, pressed && styles.paymentPressed]}>
            <CardBadge brand={card?.brand ?? null} />
            <View style={styles.paymentText}>
              <AppText variant="bodyStrong">{card ? cardLabel(card) : 'Kart ekle'}</AppText>
              <AppText variant="caption" muted>
                {card
                  ? `Son kullanma ${formatCardExpiry(card)}`
                  : cardLoading
                    ? 'Kayıtlı kart okunuyor…'
                    : 'Sürüşe başlamak için bir kart gerekli.'}
              </AppText>
            </View>
            <AppText variant="label" color={theme.textMuted}>
              {card ? 'Değiştir' : 'Ekle'}
            </AppText>
          </Pressable>
          <AppText variant="caption" muted style={styles.note}>
            Test modu: karttan gerçek ödeme alınmaz. Kart numaranın yalnızca son 4 hanesi bu telefonda saklanır.
          </AppText>
        </View>

        {start.isError ? <Notice tone="danger">{errorMessage(start.error)}</Notice> : null}
        {cancellation.isError ? <Notice tone="danger">{errorMessage(cancellation.error)}</Notice> : null}
      </ScrollView>

      <View style={[styles.footer, { paddingBottom: insets.bottom + space.md }]}>
        {card || blocked ? (
          <Button
            title="Kilidi aç ve başla"
            size="lg"
            icon="lock-open"
            disabled={!ready}
            loading={start.isPending}
            onPress={() => start.mutate()}
          />
        ) : (
          <Button title="Kart ekle" size="lg" icon="card" onPress={() => router.push('/payment-method')} />
        )}
      </View>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  root: {
    flex: 1,
    backgroundColor: t.bg,
  },
  center: {
    flex: 1,
    justifyContent: 'center',
    gap: space.md,
    padding: screenPadding,
    backgroundColor: t.bg,
  },
  content: {
    padding: screenPadding,
    paddingBottom: space.xxl,
    gap: space.lg,
  },
  vehicle: {
    gap: space.md,
  },
  vehicleHeader: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md + 2,
  },
  vehicleTitle: {
    flex: 1,
    gap: space.xs + 2,
  },
  section: {
    gap: space.sm + 2,
  },
  note: {
    paddingHorizontal: space.xs,
  },
  payment: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md + 2,
    minHeight: 68,
    paddingHorizontal: space.lg + 2,
    paddingVertical: space.md,
    borderRadius: 20,
    backgroundColor: t.surface,
  },
  paymentPressed: {
    backgroundColor: t.surfaceAlt,
  },
  paymentText: {
    flex: 1,
    gap: 2,
  },
  footer: {
    paddingTop: space.md,
    paddingHorizontal: screenPadding,
    backgroundColor: t.surface,
    borderTopWidth: 1,
    borderTopColor: t.line,
  },
}));
