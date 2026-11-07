import { createContext, use, useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';

import { useAuth } from '@/auth/AuthProvider';
import { parseSavedCard, type SavedCard } from '@/domain/card';
import { storage } from '@/lib/storage';

/**
 * Ödeme yöntemi (test modu). Backend kart almadığı için kart yalnızca bu telefonda, kullanıcı başına saklanır;
 * kayıtta tam numara ve CVC yoktur (bkz. `domain/card.ts`). Sürüş bitince ücreti sunucunun ödeme simülatörü tahsil eder.
 */
type PaymentContextValue = {
  card: SavedCard | null;
  /** Kayıtlı kart okunuyor */
  loading: boolean;
  saveCard: (card: SavedCard) => Promise<void>;
  removeCard: () => Promise<void>;
};

const PaymentContext = createContext<PaymentContextValue | null>(null);

const cardKey = (userId: string) => `scootly.card.${userId}`;

export function PaymentProvider({ children }: { children: ReactNode }) {
  const { state } = useAuth();
  const userId = state.status === 'signedIn' ? state.account.id : null;
  const [card, setCard] = useState<SavedCard | null>(null);
  const [loading, setLoading] = useState(false);

  // Oturum değişince önceki kullanıcının kartı hemen kaybolsun.
  const [loadedFor, setLoadedFor] = useState<string | null>(null);
  if (userId !== loadedFor) {
    setLoadedFor(userId);
    setCard(null);
    setLoading(userId !== null);
  }

  useEffect(() => {
    if (!userId) return;
    let cancelled = false;
    void storage.getItem(cardKey(userId)).then((value) => {
      if (cancelled) return;
      setCard(parseSavedCard(value));
      setLoading(false);
    });
    return () => {
      cancelled = true;
    };
  }, [userId]);

  const saveCard = useCallback(
    async (next: SavedCard) => {
      if (!userId) return;
      await storage.setItem(cardKey(userId), JSON.stringify(next));
      setCard(next);
    },
    [userId],
  );

  const removeCard = useCallback(async () => {
    if (!userId) return;
    await storage.removeItem(cardKey(userId));
    setCard(null);
  }, [userId]);

  const value = useMemo<PaymentContextValue>(
    () => ({ card, loading, saveCard, removeCard }),
    [card, loading, saveCard, removeCard],
  );

  return <PaymentContext value={value}>{children}</PaymentContext>;
}

export function usePayment(): PaymentContextValue {
  const context = use(PaymentContext);
  if (!context) throw new Error('usePayment, PaymentProvider içinde kullanılmalı.');
  return context;
}
