import { useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { queryKeys, usePaymentStatus, useRide } from '@/api/queries';
import type { PaymentStatus, Ride } from '@/api/types';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { Plate } from '@/components/Plate';
import { InfoRow, RowGroup } from '@/components/Rows';
import { Screen } from '@/components/Screen';
import { LoadingView } from '@/components/StateViews';
import { PaymentStatusPill, RideStatusPill } from '@/components/StatusPill';
import { Surface } from '@/components/Surface';
import { formatCurrency, formatDayMonth, formatMinutes, formatTime, parseServerDate } from '@/lib/format';
import { makeStyles, space, textVariants, useTheme } from '@/theme';

function PaymentNotice({ status }: { status: PaymentStatus }) {
  const theme = useTheme();
  switch (status) {
    case 'Pending':
      return (
        <Notice tone="info">
          <View style={{ flexDirection: 'row', alignItems: 'center', gap: space.sm }}>
            <ActivityIndicator color={theme.info.fg} />
            <AppText color={theme.info.fg} style={{ flex: 1 }}>
              Ödemen işleniyor. Bu ekranı kapatabilirsin; sonucu Sürüşlerim&apos;de görürsün.
            </AppText>
          </View>
        </Notice>
      );
    case 'Paid':
      return <Notice tone="success">Ödeme alındı. İyi yolculuklar!</Notice>;
    case 'Failed':
      return (
        <Notice tone="danger" title="Ödeme alınamadı">
          Tahsilat operasyon ekibine devredildi; seninle iletişime geçilecek.
        </Notice>
      );
    default:
      return <Notice tone="info">Sürüş devam ediyor; ücret sürüş bitince hesaplanır.</Notice>;
  }
}

/** Başlangıçtan bitişe iki duraklı zaman çizelgesi. */
function Timeline({ ride }: { ride: Ride }) {
  const styles = useStyles();
  const startedAt = parseServerDate(ride.startedAt);
  const endedAt = ride.endedAt ? parseServerDate(ride.endedAt) : null;

  return (
    <Surface style={styles.timeline}>
      <View style={styles.stop}>
        <View style={styles.rail}>
          <View style={[styles.dot, styles.dotStart]} />
          <View style={styles.line} />
        </View>
        <View style={styles.stopText}>
          <Text style={styles.time}>{formatTime(startedAt)}</Text>
          <AppText muted variant="caption">{`Başlangıç, ${formatDayMonth(startedAt)}`}</AppText>
        </View>
      </View>
      <View style={styles.stop}>
        <View style={styles.rail}>
          <View style={styles.line} />
        </View>
        <View style={[styles.stopText, styles.durationRow]}>
          <AppText variant="label" muted>
            {endedAt ? formatMinutes(endedAt.getTime() - startedAt.getTime()) : 'Sürüyor'}
          </AppText>
        </View>
      </View>
      <View style={styles.stop}>
        <View style={styles.rail}>
          <View style={[styles.line, styles.lineEnd]} />
          <View style={[styles.dot, styles.dotEnd]} />
        </View>
        <View style={styles.stopText}>
          <Text style={styles.time}>{endedAt ? formatTime(endedAt) : '—'}</Text>
          <AppText muted variant="caption">
            {endedAt ? `Bitiş, ${formatDayMonth(endedAt)}` : 'Bitiş'}
          </AppText>
        </View>
      </View>
    </Surface>
  );
}

export default function RideSummaryScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const styles = useStyles();
  const router = useRouter();
  const queryClient = useQueryClient();

  const rideQuery = useRide(id);
  const ride = rideQuery.data;
  const payment = usePaymentStatus(id, Boolean(ride && ride.status !== 'Active'));

  const paymentStatus = payment.data?.paymentStatus ?? ride?.paymentStatus;
  const fare = payment.data?.fare ?? ride?.fare ?? null;

  // Ödeme sonuçlanınca geçmiş listesini de güncelle.
  useEffect(() => {
    if (paymentStatus === 'Paid' || paymentStatus === 'Failed') {
      void queryClient.invalidateQueries({ queryKey: queryKeys.rideHistory });
    }
  }, [paymentStatus, queryClient]);

  if (rideQuery.isPending) return <LoadingView message="Sürüş yükleniyor…" />;

  if (rideQuery.isError || !ride) {
    return (
      <Screen edges={['bottom']}>
        <Notice tone="danger" action={{ label: 'Tekrar dene', onPress: () => void rideQuery.refetch() }}>
          {errorMessage(rideQuery.error, 'Sürüş bulunamadı.')}
        </Notice>
      </Screen>
    );
  }

  const active = ride.status === 'Active';

  return (
    <Screen edges={['bottom']}>
      <View
        style={styles.summary}
        accessible
        accessibilityLabel={fare !== null ? `Toplam ücret ${formatCurrency(fare)}` : undefined}>
        <AppText variant="label" muted>
          {active ? 'Ücret sürüş bitince hesaplanır' : 'Toplam ücret'}
        </AppText>
        <Text style={styles.fare}>{fare !== null ? formatCurrency(fare) : '—'}</Text>
        <View style={styles.pills}>
          <RideStatusPill status={ride.status} />
          {paymentStatus && !active ? <PaymentStatusPill status={paymentStatus} /> : null}
        </View>
      </View>

      {paymentStatus ? <PaymentNotice status={active ? 'NotYetCharged' : paymentStatus} /> : null}

      {ride.status === 'Abandoned' ? (
        <AppText muted>Uzun süre açık kalan sürüşler sistem tarafından sonlandırılır.</AppText>
      ) : null}

      <Timeline ride={ride} />

      <RowGroup>
        <InfoRow label="Scooter" value={<Plate id={ride.vehicleId} kind="Araç" />} />
        <InfoRow label="Sürüş kodu" value={<Plate id={ride.id} kind="Sürüş" />} />
      </RowGroup>

      <Button title="Haritaya dön" size="lg" icon="map" onPress={() => router.navigate('/')} />
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  summary: {
    alignItems: 'center',
    gap: space.xs,
    paddingTop: space.lg,
    paddingBottom: space.sm,
  },
  fare: {
    ...textVariants.display,
    fontSize: 64,
    lineHeight: 68,
    color: t.text,
  },
  pills: {
    flexDirection: 'row',
    gap: space.sm,
    marginTop: space.xs,
  },
  timeline: {
    gap: 0,
    paddingVertical: space.lg,
  },
  stop: {
    flexDirection: 'row',
    gap: space.md + 2,
  },
  rail: {
    width: 16,
    alignItems: 'center',
  },
  dot: {
    width: 14,
    height: 14,
    borderRadius: 7,
    borderWidth: 3,
  },
  dotStart: {
    borderColor: t.text,
    backgroundColor: t.surface,
    marginTop: 7,
  },
  dotEnd: {
    borderColor: t.text,
    backgroundColor: t.brand,
  },
  line: {
    flex: 1,
    width: 2,
    backgroundColor: t.lineStrong,
  },
  lineEnd: {
    flex: 0,
    height: 7,
  },
  stopText: {
    flex: 1,
    gap: 0,
    paddingBottom: space.xs,
  },
  durationRow: {
    paddingVertical: space.sm,
  },
  time: {
    ...textVariants.numeral,
    fontSize: 26,
    lineHeight: 30,
    color: t.text,
  },
}));
