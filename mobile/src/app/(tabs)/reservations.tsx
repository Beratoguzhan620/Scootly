import { useRouter } from 'expo-router';
import { useState } from 'react';
import { ActivityIndicator, RefreshControl, View } from 'react-native';

import { useActiveRide } from '@/api/queries';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { Screen } from '@/components/Screen';
import { EmptyState } from '@/components/StateViews';
import { Surface } from '@/components/Surface';
import { RESERVATION_DURATION_MS } from '@/domain/rules';
import { ActiveReservationCard } from '@/features/reservations/ActiveReservationCard';
import { HistoryRow } from '@/features/reservations/HistoryRow';
import { useNow } from '@/hooks/useNow';
import { useUserLocation } from '@/hooks/useUserLocation';
import { formatTime, parseServerDate } from '@/lib/format';
import { useReservation } from '@/ride/ReservationProvider';
import { makeStyles, space, useTheme } from '@/theme';

export default function ReservationsScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const router = useRouter();
  const { reservation, restoring, history, refresh, expired, dismissExpired } = useReservation();
  const activeRide = useActiveRide().data ?? null;
  const { location } = useUserLocation();
  const now = useNow(60_000);
  const [refreshing, setRefreshing] = useState(false);

  const onRefresh = async () => {
    setRefreshing(true);
    try {
      await refresh();
    } finally {
      setRefreshing(false);
    }
  };

  let current;
  if (restoring) {
    current = (
      <Surface style={styles.loading}>
        <ActivityIndicator color={theme.text} />
        <AppText muted>Rezervasyonun kontrol ediliyor…</AppText>
      </Surface>
    );
  } else if (reservation) {
    current = <ActiveReservationCard reservation={reservation} userLocation={location} />;
  } else if (!activeRide) {
    current = (
      <Surface padded={false}>
        <EmptyState
          icon="timer-outline"
          title="Şu an rezervasyonun yok"
          message={`Haritadan bir scooter seç ve ${RESERVATION_DURATION_MS / 60_000} dakikalığına kendine ayır. Scooter'ın yanındaysan QR kodunu okutup hemen başlayabilirsin.`}
          action={
            <View style={styles.emptyActions}>
              <Button title="Haritaya git" icon="map" inline onPress={() => router.navigate('/')} />
              <Button
                title="QR okut"
                icon="qr-code-outline"
                variant="secondary"
                inline
                onPress={() => router.push('/scan')}
              />
            </View>
          }
        />
      </Surface>
    );
  }

  return (
    <Screen
      title="Rezervasyonlar"
      edges={['top']}
      refreshControl={
        <RefreshControl
          refreshing={refreshing}
          onRefresh={() => void onRefresh()}
          tintColor={theme.text}
          colors={[theme.text]}
          progressBackgroundColor={theme.surface}
        />
      }>
      {activeRide ? (
        <Notice tone="brand" action={{ label: 'Haritaya dön', onPress: () => router.navigate('/') }}>
          {`Sürüştesin (başlangıç ${formatTime(parseServerDate(activeRide.startedAt))}). Sürüşü haritadan bitirebilirsin.`}
        </Notice>
      ) : null}
      {expired ? (
        <Notice tone="warning" onDismiss={dismissExpired}>
          Rezervasyonun süresi doldu; scooter yeniden müsait.
        </Notice>
      ) : null}

      {current}

      <View style={styles.section}>
        <View style={styles.sectionHeader}>
          <AppText variant="heading" accessibilityRole="header">
            Son rezervasyonlar
          </AppText>
          <AppText variant="caption" muted>
            Bu telefonda
          </AppText>
        </View>
        {history.length > 0 ? (
          <View>
            {history.map((record, index) => (
              <HistoryRow
                key={`${record.vehicleId}-${record.reservedAt}`}
                record={record}
                first={index === 0}
                last={index === history.length - 1}
                now={now}
                onPress={record.rideId ? () => router.push(`/ride/${record.rideId}`) : undefined}
              />
            ))}
          </View>
        ) : (
          <AppText muted>Biten rezervasyonların burada listelenir: sürüşe dönüşenler, iptal ettiklerin ve süresi dolanlar.</AppText>
        )}
      </View>
    </Screen>
  );
}

const useStyles = makeStyles(() => ({
  loading: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
  },
  emptyActions: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'center',
    gap: space.sm,
  },
  section: {
    gap: space.md,
    marginTop: space.sm,
  },
  sectionHeader: {
    flexDirection: 'row',
    alignItems: 'baseline',
    justifyContent: 'space-between',
    paddingHorizontal: space.xs,
  },
}));
