import { useQueryClient } from '@tanstack/react-query';
import { createContext, use, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { AppState } from 'react-native';

import { isApiStatus } from '@/api/client';
import { queryKeys } from '@/api/queries';
import { reservationsApi, ridesApi, vehiclesApi } from '@/api/scootly';
import type { Vehicle } from '@/api/types';
import { useAuth } from '@/auth/AuthProvider';
import { isReservationExpired, reservationRemainingMs } from '@/domain/reservation';
import { RESERVATION_DURATION_MS } from '@/domain/rules';
import {
  addRecord,
  parseHistory,
  serializeHistory,
  type ReservationOutcome,
  type ReservationRecord,
} from '@/domain/reservationHistory';
import { parseServerDate } from '@/lib/format';
import { storage } from '@/lib/storage';

/**
 * Sürücünün rezervasyonu. Kaynak sunucudur: `GET /api/reservations/active` uygulama açılınca, öne gelince ve
 * rezervasyon sürerken dakikada bir sorulur; böylece rezervasyon uygulama kapatılıp açılınca ya da başka bir
 * telefondan girilince de görünür. Bu uç olmayan eski bir sunucuda rezerve edilen araç ve zamanı cihazda saklanır ve
 * araç ayrıntısıyla doğrulanır (müsait olmayan araç yalnızca onu rezerve etmiş sürücüye görünür).
 */
export type StoredReservation = {
  vehicleId: string;
  /** epoch ms; sunucudan geldiyse sunucunun kaydettiği an */
  reservedAt: number;
  /** Geçmiş listesinde göstermek için */
  brand?: string;
};

type VehicleRef = Pick<Vehicle, 'id' | 'brand'>;

type ReservationContextValue = {
  reservation: StoredReservation | null;
  /** Oturum açılınca rezervasyon sunucudan yükleniyor. */
  restoring: boolean;
  /** true: sunucu rezervasyonu tutuyor · false: eski sunucu, kayıt bu telefonda · null: henüz bilinmiyor */
  serverBacked: boolean | null;
  /** Son rezervasyon süre dolduğu için düştüyse true (bilgi mesajı için). */
  expired: boolean;
  dismissExpired: () => void;
  /** Bu telefondaki son rezervasyonlar, yeniden eskiye */
  history: ReservationRecord[];
  reserve: (vehicle: VehicleRef) => Promise<void>;
  cancel: () => Promise<void>;
  /**
   * Sürüşü başlatır ve sürüş kimliğini döner. Araç bu sürücüye ayrılmamışsa (QR ile doğrudan başlatma) önce rezerve
   * edilir; sunucu sürüşü yalnızca rezervasyon sahibine başlatır.
   */
  startRide: (vehicle: VehicleRef, payment: { card: string }) => Promise<string>;
  /** Sunucudaki durumla eşitle. */
  refresh: () => Promise<void>;
};

const ReservationContext = createContext<ReservationContextValue | null>(null);

const storageKey = (userId: string) => `scootly.reservation.${userId}`;
const historyKey = (userId: string) => `scootly.reservations.${userId}`;

/** Rezervasyon sürerken sunucuya bu aralıkla sorulur (filo aracı bakıma alırsa rezervasyon düşer). */
const SYNC_INTERVAL_MS = 60_000;

function parseStored(value: string | null): StoredReservation | null {
  if (!value) return null;
  try {
    const parsed = JSON.parse(value) as Partial<StoredReservation>;
    if (typeof parsed.vehicleId !== 'string' || typeof parsed.reservedAt !== 'number') return null;
    return {
      vehicleId: parsed.vehicleId,
      reservedAt: parsed.reservedAt,
      brand: typeof parsed.brand === 'string' ? parsed.brand : undefined,
    };
  } catch {
    return null;
  }
}

type Reconciled = { reservation: StoredReservation | null; serverBacked: boolean };

/**
 * Sunucudaki duruma göre rezervasyonu belirler. Sunucuya ulaşılamazsa null döner: cihazdaki kayıt korunur ve
 * geri sayım sürer.
 */
async function reconcile(base: StoredReservation | null): Promise<Reconciled | null> {
  let server;
  try {
    server = await reservationsApi.active();
  } catch {
    return null;
  }

  if (server.kind === 'active') {
    const { reservation } = server;
    return {
      serverBacked: true,
      reservation: {
        vehicleId: reservation.vehicleId,
        reservedAt: parseServerDate(reservation.reservedAt).getTime(),
        brand: reservation.vehicle.brand,
      },
    };
  }
  if (server.kind === 'none') return { serverBacked: true, reservation: null };

  // Eski sunucu: cihazdaki kaydı aracın görünürlüğüyle doğrula.
  if (!base || isReservationExpired(base.reservedAt, Date.now())) return { serverBacked: false, reservation: null };
  try {
    const vehicle = await vehiclesApi.get(base.vehicleId);
    return { serverBacked: false, reservation: vehicle.status === 'Reserved' ? base : null };
  } catch (error) {
    // 404: araç artık bu sürücüye ayrılmış değil. Ağ hatasında kayıt korunur.
    return isApiStatus(error, 404) ? { serverBacked: false, reservation: null } : null;
  }
}

export function ReservationProvider({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  // Rezervasyon yalnızca sürücü modunda; filo ekibinin sürücü uçlarına yetkisi yok.
  const userId = state.status === 'signedIn' && state.mode === 'driver' ? state.account.id : null;
  const queryClient = useQueryClient();

  const [reservation, setReservation] = useState<StoredReservation | null>(null);
  const [history, setHistory] = useState<ReservationRecord[]>([]);
  const [restoring, setRestoring] = useState(false);
  const [serverBacked, setServerBacked] = useState<boolean | null>(null);
  const [expired, setExpired] = useState(false);

  /** İşleyicilerin okuduğu güncel değerler (render sırasında okunmaz). */
  const reservationRef = useRef<StoredReservation | null>(null);
  const historyRef = useRef<ReservationRecord[]>([]);
  /** Her yerel değişiklikte artar; eşitleme sürerken kullanıcı bir şey yaptıysa eşitlemenin sonucu atılır. */
  const versionRef = useRef(0);

  // Oturum değişince önceki kullanıcının durumunu hemen sıfırla (React'in "prop değişince state ayarla" kalıbı).
  const [loadedFor, setLoadedFor] = useState<string | null>(null);
  if (userId !== loadedFor) {
    setLoadedFor(userId);
    setReservation(null);
    setHistory([]);
    setExpired(false);
    setServerBacked(null);
    setRestoring(userId !== null);
  }

  const persist = useCallback(
    async (next: StoredReservation | null) => {
      reservationRef.current = next;
      setReservation(next);
      if (!userId) return;
      if (next) await storage.setItem(storageKey(userId), JSON.stringify(next));
      else await storage.removeItem(storageKey(userId));
    },
    [userId],
  );

  const record = useCallback(
    async (ended: StoredReservation, outcome: ReservationOutcome, extra: { rideId?: string; card?: string } = {}) => {
      const next = addRecord(historyRef.current, {
        vehicleId: ended.vehicleId,
        brand: ended.brand,
        reservedAt: ended.reservedAt,
        // Süresi dolan rezervasyon, uygulama kapalıyken dolmuş olsa da dolduğu anda bitmiştir.
        endedAt:
          outcome === 'expired' ? Math.min(Date.now(), ended.reservedAt + RESERVATION_DURATION_MS) : Date.now(),
        outcome,
        ...extra,
      });
      historyRef.current = next;
      setHistory(next);
      if (userId) await storage.setItem(historyKey(userId), serializeHistory(next)).catch(() => undefined);
    },
    [userId],
  );

  const refreshVehicles = useCallback(
    () => queryClient.invalidateQueries({ queryKey: queryKeys.vehicles }),
    [queryClient],
  );

  /** Sunucudaki durumu uygular, biten rezervasyonu geçmişe yazar ve geçerli rezervasyonu döner. */
  const apply = useCallback(
    async (base: StoredReservation | null, result: Reconciled): Promise<StoredReservation | null> => {
      setServerBacked(result.serverBacked);
      const next = result.reservation ? { ...result.reservation, brand: result.reservation.brand ?? base?.brand } : null;
      if (base && next?.vehicleId !== base.vehicleId) {
        if (isReservationExpired(base.reservedAt, Date.now())) {
          await record(base, 'expired');
        } else {
          // Sürüşe dönüşmüş olabilir (ör. başka bir telefondan başlatıldı).
          const ride = await ridesApi.active().catch(() => null);
          await record(base, ride?.vehicleId === base.vehicleId ? 'ride' : 'released', { rideId: ride?.id });
        }
      }
      if (next?.vehicleId !== base?.vehicleId || next?.reservedAt !== base?.reservedAt) await persist(next);
      return next;
    },
    [persist, record],
  );

  const refresh = useCallback(async () => {
    if (!userId) return;
    const version = versionRef.current;
    const base = reservationRef.current;
    const result = await reconcile(base);
    if (!result || version !== versionRef.current) return;
    await apply(base, result);
  }, [userId, apply]);

  // Oturum açılınca: cihazdaki kaydı ve geçmişi oku, sunucuyla eşitle.
  useEffect(() => {
    if (!userId) return;
    let cancelled = false;

    (async () => {
      const [storedValue, historyValue] = await Promise.all([
        storage.getItem(storageKey(userId)),
        storage.getItem(historyKey(userId)),
      ]);
      if (cancelled) return;
      const stored = parseStored(storedValue);
      historyRef.current = parseHistory(historyValue);
      setHistory(historyRef.current);
      reservationRef.current = stored;

      const version = versionRef.current;
      const result = await reconcile(stored);
      if (cancelled) return;

      let current: StoredReservation | null;
      if (version !== versionRef.current) {
        // Yüklenirken kullanıcı rezerve etti ya da iptal etti; onun sonucu geçerli.
        current = reservationRef.current;
      } else if (result) {
        current = await apply(stored, result);
      } else {
        // Sunucuya ulaşılamadı: süresi dolmamışsa cihazdaki kayıtla devam et.
        current = stored && !isReservationExpired(stored.reservedAt, Date.now()) ? stored : null;
      }
      if (cancelled) return;
      reservationRef.current = current;
      setReservation(current);
      setRestoring(false);
    })();

    return () => {
      cancelled = true;
    };
  }, [userId, apply]);

  // Uygulama öne gelince ve rezervasyon sürerken arada bir sunucuyla eşitle.
  useEffect(() => {
    if (!userId) return;
    const subscription = AppState.addEventListener('change', (status) => {
      if (status === 'active') void refresh();
    });
    const interval = reservation ? setInterval(() => void refresh(), SYNC_INTERVAL_MS) : null;
    return () => {
      subscription.remove();
      if (interval) clearInterval(interval);
    };
  }, [userId, reservation, refresh]);

  // Süre dolunca yerel kaydı düşür; sunucuda Worker aynı rezervasyonu kısa süre içinde serbest bırakır.
  useEffect(() => {
    if (!reservation) return;
    const timer = setTimeout(
      () => {
        versionRef.current += 1;
        void record(reservation, 'expired');
        void persist(null);
        setExpired(true);
        void refreshVehicles();
      },
      reservationRemainingMs(reservation.reservedAt, Date.now()) + 1_000,
    );
    return () => clearTimeout(timer);
  }, [reservation, persist, record, refreshVehicles]);

  const reserve = useCallback(
    async (vehicle: VehicleRef) => {
      versionRef.current += 1;
      await vehiclesApi.reserve(vehicle.id);
      setExpired(false);
      await persist({ vehicleId: vehicle.id, reservedAt: Date.now(), brand: vehicle.brand });
      await refreshVehicles();
      // Sunucunun kaydettiği zamanı al (geri sayım sunucuyla aynı olsun).
      void refresh();
    },
    [persist, refreshVehicles, refresh],
  );

  const cancel = useCallback(async () => {
    const current = reservationRef.current;
    if (!current) return;
    versionRef.current += 1;
    try {
      await vehiclesApi.cancelReservation(current.vehicleId);
    } catch (error) {
      // 404/409: rezervasyon sunucuda zaten düşmüş (süre dolmuş ya da sürüş başlamış); yerelde de temizle.
      if (!isApiStatus(error, 404, 409)) throw error;
    }
    await record(current, 'cancelled');
    await persist(null);
    await refreshVehicles();
  }, [persist, record, refreshVehicles]);

  const startRide = useCallback(
    async (vehicle: VehicleRef, payment: { card: string }) => {
      versionRef.current += 1;
      let current = reservationRef.current;
      if (current && current.vehicleId !== vehicle.id) {
        throw new Error('Başka bir scooter için rezervasyonun var. Önce onu iptal et ya da o scooter ile başla.');
      }
      if (!current) {
        await vehiclesApi.reserve(vehicle.id);
        current = { vehicleId: vehicle.id, reservedAt: Date.now(), brand: vehicle.brand };
        await persist(current);
      }

      try {
        const { rideId } = await ridesApi.start(vehicle.id);
        await record({ ...current, brand: current.brand ?? vehicle.brand }, 'ride', { rideId, card: payment.card });
        await persist(null);
        await Promise.all([queryClient.invalidateQueries({ queryKey: queryKeys.rides }), refreshVehicles()]);
        return rideId;
      } catch (error) {
        // "Araç rezerve edilmemiş" (409): süre dolmuş; yerel kaydı da düşür ki kullanıcı yeniden rezerve edebilsin.
        if (isApiStatus(error, 404, 409)) {
          await queryClient.invalidateQueries({ queryKey: queryKeys.activeRide });
          const active = await ridesApi.active().catch(() => null);
          if (!active) await persist(null);
        }
        throw error;
      }
    },
    [persist, record, queryClient, refreshVehicles],
  );

  const dismissExpired = useCallback(() => setExpired(false), []);

  const value = useMemo<ReservationContextValue>(
    () => ({
      reservation,
      restoring,
      serverBacked,
      expired,
      dismissExpired,
      history,
      reserve,
      cancel,
      startRide,
      refresh,
    }),
    [reservation, restoring, serverBacked, expired, dismissExpired, history, reserve, cancel, startRide, refresh],
  );

  return <ReservationContext value={value}>{children}</ReservationContext>;
}

export function useReservation(): ReservationContextValue {
  const context = use(ReservationContext);
  if (!context) throw new Error('useReservation, ReservationProvider içinde kullanılmalı.');
  return context;
}
