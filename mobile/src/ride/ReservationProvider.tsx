import { useQueryClient } from '@tanstack/react-query';
import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';

import { isApiStatus } from '@/api/client';
import { queryKeys } from '@/api/queries';
import { ridesApi, vehiclesApi } from '@/api/scootly';
import { useAuth } from '@/auth/AuthProvider';
import { isReservationExpired, reservationRemainingMs } from '@/domain/reservation';
import { storage } from '@/lib/storage';

/**
 * API'de "aktif rezervasyonum" ucu yok (sürüş için `GET /api/rides/active` var). Uygulama kapanıp açılınca
 * rezervasyonu kaybetmemek için rezerve edilen araç ve zamanı cihazda saklanır; açılışta araç ayrıntısıyla
 * doğrulanır (müsait olmayan araç yalnızca onu rezerve etmiş sürücüye görünür).
 */
export type StoredReservation = {
  vehicleId: string;
  /** İstemci saatine göre epoch ms; geri sayım yaklaşık olabilir. */
  reservedAt: number;
};

type ReservationContextValue = {
  reservation: StoredReservation | null;
  /** Kayıtlı rezervasyon doğrulanıyor. */
  restoring: boolean;
  /** Son rezervasyon süre dolduğu için düştüyse true (bilgi mesajı için). */
  expired: boolean;
  dismissExpired: () => void;
  reserve: (vehicleId: string) => Promise<void>;
  cancel: () => Promise<void>;
  /** Rezerve edilen araçla sürüşü başlatır, sürüş kimliğini döner. */
  startRide: () => Promise<string>;
};

const ReservationContext = createContext<ReservationContextValue | null>(null);

const storageKey = (userId: string) => `scootly.reservation.${userId}`;

function parseStored(value: string | null): StoredReservation | null {
  if (!value) return null;
  try {
    const parsed = JSON.parse(value) as Partial<StoredReservation>;
    return typeof parsed.vehicleId === 'string' && typeof parsed.reservedAt === 'number'
      ? { vehicleId: parsed.vehicleId, reservedAt: parsed.reservedAt }
      : null;
  } catch {
    return null;
  }
}

export function ReservationProvider({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  const userId = state.status === 'signedIn' ? state.account.id : null;
  const queryClient = useQueryClient();

  const [reservation, setReservation] = useState<StoredReservation | null>(null);
  const [restoring, setRestoring] = useState(false);
  const [expired, setExpired] = useState(false);

  // Oturum değişince önceki kullanıcının durumunu hemen sıfırla (React'in "prop değişince state ayarla" kalıbı).
  const [loadedFor, setLoadedFor] = useState<string | null>(null);
  if (userId !== loadedFor) {
    setLoadedFor(userId);
    setReservation(null);
    setExpired(false);
    setRestoring(userId !== null);
  }

  const persist = useCallback(
    async (next: StoredReservation | null) => {
      setReservation(next);
      if (!userId) return;
      if (next) await storage.setItem(storageKey(userId), JSON.stringify(next));
      else await storage.removeItem(storageKey(userId));
    },
    [userId],
  );

  const refreshVehicles = useCallback(
    () => queryClient.invalidateQueries({ queryKey: queryKeys.vehicles }),
    [queryClient],
  );

  // Oturum açılınca kayıtlı rezervasyonu yükle ve sunucuyla doğrula.
  useEffect(() => {
    if (!userId) return;

    let cancelled = false;

    (async () => {
      const stored = parseStored(await storage.getItem(storageKey(userId)));
      let valid = stored !== null && !isReservationExpired(stored.reservedAt, Date.now());

      if (stored && valid) {
        try {
          const vehicle = await vehiclesApi.get(stored.vehicleId);
          valid = vehicle.status === 'Reserved';
        } catch (error) {
          // 404: araç artık bu sürücüye ayrılmış değil. Ağ hatasında kaydı koru, geri sayım devam etsin.
          valid = !isApiStatus(error, 404);
        }
      }

      if (cancelled) return;
      if (stored && valid) setReservation(stored);
      else if (stored) await storage.removeItem(storageKey(userId));
      setRestoring(false);
    })();

    return () => {
      cancelled = true;
    };
  }, [userId]);

  // Süre dolunca yerel kaydı düşür; sunucuda Worker aynı rezervasyonu kısa süre içinde serbest bırakır.
  useEffect(() => {
    if (!reservation) return;
    const timer = setTimeout(
      () => {
        void persist(null);
        setExpired(true);
        void refreshVehicles();
      },
      reservationRemainingMs(reservation.reservedAt, Date.now()) + 1_000,
    );
    return () => clearTimeout(timer);
  }, [reservation, persist, refreshVehicles]);

  const reserve = useCallback(
    async (vehicleId: string) => {
      await vehiclesApi.reserve(vehicleId);
      setExpired(false);
      await persist({ vehicleId, reservedAt: Date.now() });
      await refreshVehicles();
    },
    [persist, refreshVehicles],
  );

  const cancel = useCallback(async () => {
    if (!reservation) return;
    try {
      await vehiclesApi.cancelReservation(reservation.vehicleId);
    } catch (error) {
      // 404/409: rezervasyon sunucuda zaten düşmüş (süre dolmuş ya da sürüş başlamış); yerelde de temizle.
      if (!isApiStatus(error, 404, 409)) throw error;
    }
    await persist(null);
    await refreshVehicles();
  }, [reservation, persist, refreshVehicles]);

  const startRide = useCallback(async () => {
    if (!reservation) throw new Error('Önce bir araç rezerve edin.');
    try {
      const { rideId } = await ridesApi.start(reservation.vehicleId);
      await persist(null);
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.rides }),
        refreshVehicles(),
      ]);
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
  }, [reservation, persist, queryClient, refreshVehicles]);

  const dismissExpired = useCallback(() => setExpired(false), []);

  const value = useMemo<ReservationContextValue>(
    () => ({ reservation, restoring, expired, dismissExpired, reserve, cancel, startRide }),
    [reservation, restoring, expired, dismissExpired, reserve, cancel, startRide],
  );

  return <ReservationContext value={value}>{children}</ReservationContext>;
}

export function useReservation(): ReservationContextValue {
  const context = use(ReservationContext);
  if (!context) throw new Error('useReservation, ReservationProvider içinde kullanılmalı.');
  return context;
}
