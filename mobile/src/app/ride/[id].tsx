import { useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useEffect } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { queryKeys, usePaymentStatus, useRide } from '@/api/queries';
import type { PaymentStatus, Ride } from '@/api/types';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Icon, type IconName } from '@/components/Icon';
import { Notice } from '@/components/Notice';
import { Plate } from '@/components/Plate';
import { InfoRow, RowGroup } from '@/components/Rows';
import { Screen } from '@/components/Screen';
import { LoadingView } from '@/components/StateViews';
import { Surface } from '@/components/Surface';
import { billableMinutes, roundMoney } from '@/domain/fare';
import { STANDARD_TARIFF } from '@/domain/rules';
import { formatCurrency, formatDayMonth, formatMinutes, formatTime, parseServerDate } from '@/lib/format';
import { haptics } from '@/lib/haptics';
import { useReservation } from '@/ride/ReservationProvider';
import { makeStyles, radius, space, textVariants, toneColors, useTheme, type Tone } from '@/theme';

type Stage = { title: string; detail: string; icon: IconName; tone: Tone; busy?: boolean };

/** Ödemenin bulunduğu aşama: büyük simge, başlık ve açıklama. */
function stageFor(ride: Ride, status: PaymentStatus | undefined): Stage {
  if (ride.status === 'Active') {
    return { title: 'Sürüş devam ediyor', detail: 'Ücret sürüş bitince hesaplanır.', icon: 'navigate', tone: 'primary' };
  }
  switch (status) {
    case 'Paid':
      return { title: 'Ödeme alındı', detail: 'İyi yolculuklar!', icon: 'checkmark', tone: 'success' };
    case 'Failed':
      return {
        title: 'Ödeme alınamadı',
        detail: 'Tahsilat operasyon ekibine devredildi; seninle iletişime geçilecek.',
        icon: 'close',
        tone: 'danger',
      };
    case 'Pending':
      return {
        title: 'Ödeme alınıyor',
        detail: "Bu ekranı kapatabilirsin; sonucu Sürüşlerim'de görürsün.",
        icon: 'card',
        tone: 'primary',
        busy: true,
      };
    default:
      return {
        title: ride.status === 'Abandoned' ? 'Sürüşü sistem sonlandırdı' : 'Sürüş bitti',
        detail: 'Ücret henüz tahsil edilmedi.',
        icon: 'flag',
        tone: 'secondary',
      };
  }
}

/** Sürüş bitti → ödeme işleniyor → ödeme alındı: üç adımlı ilerleme. */
function PaymentSteps({ ride, status }: { ride: Ride; status: PaymentStatus | undefined }) {
  const theme = useTheme();
  const styles = useStyles();
  const endedAt = ride.endedAt ? parseServerDate(ride.endedAt) : null;
  const failed = status === 'Failed';
  const processing = !endedAt ? 'todo' : status === 'Pending' || status === 'NotYetCharged' ? 'current' : 'done';
  const steps: { label: string; detail?: string; state: 'done' | 'current' | 'todo' | 'failed' }[] = [
    { label: 'Sürüş bitti', detail: endedAt ? formatTime(endedAt) : undefined, state: endedAt ? 'done' : 'current' },
    { label: processing === 'done' ? 'Ödeme işlendi' : 'Ödeme işleniyor', state: processing },
    {
      label: failed ? 'Ödeme alınamadı' : 'Ödeme alındı',
      state: status === 'Paid' ? 'done' : failed ? 'failed' : 'todo',
    },
  ];

  return (
    <View style={styles.steps} accessibilityRole="list">
      {steps.map((step, index) => {
        const colors =
          step.state === 'failed'
            ? toneColors(theme, 'danger')
            : step.state === 'done'
              ? toneColors(theme, 'success')
              : toneColors(theme, 'secondary');
        return (
          <View
            key={step.label}
            style={styles.step}
            accessible
            accessibilityLabel={`${step.label}${step.state === 'done' ? ', tamamlandı' : step.state === 'current' ? ', sürüyor' : ''}`}>
            <View style={styles.stepRail}>
              <View
                style={[
                  styles.stepDot,
                  { backgroundColor: step.state === 'todo' ? theme.surfaceAlt : colors.solid },
                  step.state === 'current' && { backgroundColor: theme.brand },
                ]}>
                {step.state === 'done' ? <Icon name="checkmark" size={14} color="#FFFFFF" /> : null}
                {step.state === 'failed' ? <Icon name="close" size={14} color="#FFFFFF" /> : null}
                {step.state === 'current' ? <ActivityIndicator size="small" color={theme.onBrand} /> : null}
              </View>
              {index < steps.length - 1 ? <View style={styles.stepLine} /> : null}
            </View>
            <View style={[styles.stepText, index === steps.length - 1 && styles.stepTextLast]}>
              <AppText variant="bodyStrong" muted={step.state === 'todo'}>
                {step.label}
              </AppText>
              {step.detail ? (
                <AppText variant="caption" muted>
                  {step.detail}
                </AppText>
              ) : null}
            </View>
          </View>
        );
      })}
    </View>
  );
}

export default function RidePaymentScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const theme = useTheme();
  const styles = useStyles();
  const router = useRouter();
  const queryClient = useQueryClient();
  const { history } = useReservation();

  const rideQuery = useRide(id);
  const ride = rideQuery.data;
  const payment = usePaymentStatus(id, Boolean(ride && ride.status !== 'Active'));

  const paymentStatus = payment.data?.paymentStatus ?? ride?.paymentStatus;
  const fare = payment.data?.fare ?? ride?.fare ?? null;
  /** Sürüş bu telefondan başlatıldıysa seçilen kart */
  const card = history.find((record) => record.rideId === id)?.card;

  // Ödeme sonuçlanınca geçmiş listesini de güncelle ve hissettir.
  useEffect(() => {
    if (paymentStatus === 'Paid' || paymentStatus === 'Failed') {
      void queryClient.invalidateQueries({ queryKey: queryKeys.rideHistory });
    }
  }, [paymentStatus, queryClient]);
  const settled = payment.data?.paymentStatus;
  useEffect(() => {
    if (settled === 'Paid') haptics.success();
    else if (settled === 'Failed') haptics.error();
  }, [settled]);

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

  const startedAt = parseServerDate(ride.startedAt);
  const endedAt = ride.endedAt ? parseServerDate(ride.endedAt) : null;
  const durationMs = endedAt ? endedAt.getTime() - startedAt.getTime() : null;
  const minutes = durationMs !== null ? billableMinutes(durationMs) : null;
  // Tarife dökümü yalnızca sunucunun ücretiyle tutuyorsa gösterilir (tarife değişmiş olabilir).
  const breakdownMatches =
    minutes !== null &&
    fare !== null &&
    roundMoney(STANDARD_TARIFF.unlockFee + minutes * STANDARD_TARIFF.perMinuteRate) === fare;
  const stage = stageFor(ride, paymentStatus);
  const stageColors = toneColors(theme, stage.tone);

  return (
    <Screen edges={['bottom']}>
      <View style={styles.hero} accessible accessibilityLabel={`${stage.title}. ${fare !== null ? `Tutar ${formatCurrency(fare)}` : ''}`}>
        <View style={[styles.badge, { backgroundColor: stage.tone === 'primary' ? theme.brand : stageColors.solid }]}>
          {stage.busy ? (
            <ActivityIndicator color={theme.onBrand} size="large" />
          ) : (
            <Icon name={stage.icon} size={34} color={stage.tone === 'primary' ? theme.onBrand : '#FFFFFF'} />
          )}
        </View>
        <AppText variant="heading" align="center">
          {stage.title}
        </AppText>
        <Text style={styles.fare}>{fare !== null ? formatCurrency(fare) : '—'}</Text>
        {card ? (
          <View style={styles.cardLine}>
            <Icon name="card-outline" size={18} color={theme.textMuted} />
            <AppText muted>{card}</AppText>
          </View>
        ) : null}
        <AppText variant="caption" muted align="center" style={styles.heroDetail}>
          {stage.detail}
        </AppText>
      </View>

      {ride.status !== 'Active' ? (
        <Surface>
          <PaymentSteps ride={ride} status={paymentStatus} />
        </Surface>
      ) : null}

      <RowGroup>
        <InfoRow
          label="Süre"
          value={durationMs !== null ? formatMinutes(durationMs) : `${formatTime(startedAt)}'den beri`}
        />
        {breakdownMatches && minutes !== null ? (
          <InfoRow label={`${minutes} dk × ${formatCurrency(STANDARD_TARIFF.perMinuteRate)}`} value={formatCurrency(minutes * STANDARD_TARIFF.perMinuteRate)} />
        ) : null}
        {breakdownMatches ? (
          <InfoRow
            label="Kilit açma"
            value={STANDARD_TARIFF.unlockFee > 0 ? formatCurrency(STANDARD_TARIFF.unlockFee) : 'Ücretsiz'}
          />
        ) : null}
        <InfoRow label="Toplam" value={fare !== null ? formatCurrency(fare) : '—'} />
        {card ? <InfoRow label="Ödeme yöntemi" value={card} /> : null}
      </RowGroup>

      <RowGroup>
        <InfoRow label="Başlangıç" value={`${formatDayMonth(startedAt)} ${formatTime(startedAt)}`} />
        <InfoRow label="Bitiş" value={endedAt ? `${formatDayMonth(endedAt)} ${formatTime(endedAt)}` : '—'} />
        <InfoRow label="Scooter" value={<Plate id={ride.vehicleId} kind="Araç" />} />
        <InfoRow label="Sürüş kodu" value={<Plate id={ride.id} kind="Sürüş" />} />
      </RowGroup>

      {ride.status === 'Abandoned' ? (
        <AppText variant="caption" muted>
          Uzun süre açık kalan sürüşler sistem tarafından sonlandırılır.
        </AppText>
      ) : null}

      {/* navigate yeni bir harita açar; dismissTo yığındaki haritaya geri döner. */}
      <Button title="Haritaya dön" size="lg" icon="map" onPress={() => router.dismissTo('/')} />
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  hero: {
    alignItems: 'center',
    gap: space.xs,
    paddingTop: space.md,
    paddingBottom: space.sm,
  },
  badge: {
    width: 72,
    height: 72,
    borderRadius: 36,
    alignItems: 'center',
    justifyContent: 'center',
    marginBottom: space.sm,
  },
  fare: {
    ...textVariants.display,
    fontSize: 60,
    lineHeight: 64,
    color: t.text,
  },
  cardLine: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.xs + 2,
  },
  heroDetail: {
    maxWidth: 300,
    marginTop: space.xs,
  },
  steps: {
    gap: 0,
  },
  step: {
    flexDirection: 'row',
    gap: space.md,
    minHeight: 48,
  },
  stepRail: {
    alignItems: 'center',
    width: 26,
  },
  stepDot: {
    width: 26,
    height: 26,
    borderRadius: 13,
    alignItems: 'center',
    justifyContent: 'center',
  },
  stepLine: {
    flex: 1,
    width: 2,
    marginVertical: 3,
    borderRadius: radius.pill,
    backgroundColor: t.lineStrong,
  },
  stepText: {
    flex: 1,
    gap: 1,
    paddingTop: 2,
    paddingBottom: space.md,
  },
  stepTextLast: {
    paddingBottom: 0,
  },
}));
