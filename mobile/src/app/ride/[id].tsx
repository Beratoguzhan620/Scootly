import { useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect } from 'react';
import { ActivityIndicator, StyleSheet, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { queryKeys, usePaymentStatus, useRide } from '@/api/queries';
import type { PaymentStatus, Ride } from '@/api/types';
import { AlertBox } from '@/components/AlertBox';
import { AppText, Code } from '@/components/AppText';
import { PaymentStatusBadge, RideStatusBadge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { KeyValueRow, ListGroup } from '@/components/ListGroup';
import { Screen } from '@/components/Screen';
import { LoadingView } from '@/components/StateViews';
import { formatCurrency, formatDateTime, formatDuration, parseServerDate, shortId } from '@/lib/format';
import { colors, spacing, subtle } from '@/theme';

function PaymentNotice({ status }: { status: PaymentStatus }) {
  switch (status) {
    case 'Pending':
      return (
        <AlertBox tone="info">
          <View style={styles.pending}>
            <ActivityIndicator color={subtle.info.text} />
            <AppText color={subtle.info.text} style={styles.flex}>
              Ödemen işleniyor. Bu ekranı kapatabilirsin; sonucu Sürüşlerim&apos;de görürsün.
            </AppText>
          </View>
        </AlertBox>
      );
    case 'Paid':
      return <AlertBox tone="success">Ödeme alındı. İyi yolculuklar!</AlertBox>;
    case 'Failed':
      return (
        <AlertBox tone="danger" title="Ödeme alınamadı">
          Tahsilat operasyon ekibine devredildi; seninle iletişime geçilecek.
        </AlertBox>
      );
    default:
      return <AlertBox tone="info">Sürüş devam ediyor; ücret sürüş bitince hesaplanır.</AlertBox>;
  }
}

function RideDetails({ ride }: { ride: Ride }) {
  const startedAt = parseServerDate(ride.startedAt);
  const endedAt = ride.endedAt ? parseServerDate(ride.endedAt) : null;

  return (
    <Card header="Ayrıntılar" bodyStyle={styles.detailsBody}>
      <ListGroup flush>
        <KeyValueRow label="Başlangıç" value={formatDateTime(startedAt)} />
        <KeyValueRow label="Bitiş" value={endedAt ? formatDateTime(endedAt) : '—'} />
        <KeyValueRow
          label="Süre"
          value={endedAt ? formatDuration(endedAt.getTime() - startedAt.getTime()) : '—'}
        />
        <KeyValueRow label="Araç" value={<Code>{shortId(ride.vehicleId)}</Code>} />
        <KeyValueRow label="Sürüş" value={<Code>{shortId(ride.id)}</Code>} />
      </ListGroup>
    </Card>
  );
}

export default function RideSummaryScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
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

  if (rideQuery.isPending) return <LoadingView message="Sürüş yükleniyor..." />;

  if (rideQuery.isError || !ride) {
    return (
      <Screen edges={['bottom']}>
        <AlertBox tone="danger" action={{ label: 'Tekrar dene', onPress: () => void rideQuery.refetch() }}>
          {errorMessage(rideQuery.error, 'Sürüş bulunamadı.')}
        </AlertBox>
      </Screen>
    );
  }

  return (
    <Screen edges={['bottom']}>
      <View style={styles.summary}>
        <AppText variant="small" muted>
          {ride.status === 'Active' ? 'Tahmini ücret sürüş bitince hesaplanır' : 'Toplam ücret'}
        </AppText>
        <AppText variant="h1" style={styles.fare}>
          {fare !== null ? formatCurrency(fare) : '—'}
        </AppText>
        <View style={styles.badges}>
          <RideStatusBadge status={ride.status} />
          {paymentStatus && ride.status !== 'Active' ? <PaymentStatusBadge status={paymentStatus} /> : null}
        </View>
      </View>

      {paymentStatus ? <PaymentNotice status={ride.status === 'Active' ? 'NotYetCharged' : paymentStatus} /> : null}

      {ride.status === 'Abandoned' ? (
        <AppText muted>Uzun süre açık kalan sürüşler sistem tarafından sonlandırılır.</AppText>
      ) : null}

      <RideDetails ride={ride} />

      <Button title="Haritaya Dön" onPress={() => router.navigate('/')} />
    </Screen>
  );
}

const styles = StyleSheet.create({
  summary: {
    alignItems: 'center',
    gap: spacing[1],
    paddingVertical: spacing[3],
  },
  fare: {
    fontSize: 40,
    lineHeight: 48,
    color: colors.body,
  },
  badges: {
    flexDirection: 'row',
    gap: spacing[2],
  },
  pending: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing[2],
  },
  flex: {
    flex: 1,
  },
  detailsBody: {
    paddingVertical: spacing[1],
  },
});
