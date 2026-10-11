import { useQueryClient } from '@tanstack/react-query';
import { createContext, use, useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from 'react';
import { AppState } from 'react-native';

import { accountFromToken, isDriver } from './account';
import { configureApi, isApiStatus } from '@/api/client';
import { accountApi, authApi } from '@/api/scootly';
import type { Account } from '@/api/types';
import { appConfig } from '@/config';
import { LOCKOUT_MAX_ATTEMPTS, LOCKOUT_MINUTES } from '@/domain/rules';
import { isJwtExpired } from '@/lib/jwt';
import { storage } from '@/lib/storage';

const TOKEN_KEY = 'scootly.token';

export type Notice = { tone: 'info' | 'success' | 'warning'; message: string };

export const notices = {
  sessionExpired: {
    tone: 'warning',
    message:
      'Oturumunuzun süresi doldu. Lütfen tekrar giriş yapın. Devam eden bir sürüşünüz varsa giriş yaptıktan sonra kaldığınız yerden devam edersiniz.',
  },
  sessionEnded: {
    tone: 'warning',
    message: 'Oturumunuz sonlandırıldı (parola ya da yetki değişmiş olabilir). Lütfen tekrar giriş yapın.',
  },
  notDriver: {
    tone: 'warning',
    message: 'Bu uygulama sürücü hesapları içindir. Filo ve saha ekibi web panelini kullanmalıdır.',
  },
  accountDeleted: { tone: 'success', message: 'Hesabınız silindi.' },
} satisfies Record<string, Notice>;

export const INVALID_CREDENTIALS_MESSAGE = `E-posta veya parola hatalı. ${LOCKOUT_MAX_ATTEMPTS} hatalı denemede hesap ${LOCKOUT_MINUTES} dakika kilitlenir.`;

export type AuthState =
  | { status: 'restoring' }
  | { status: 'signedOut' }
  | { status: 'signedIn'; token: string; account: Account };

type AuthContextValue = {
  state: AuthState;
  /** Giriş ekranında gösterilecek bilgi (oturum süresi doldu, hesap silindi...). */
  notice: Notice | null;
  clearNotice: () => void;
  signIn: (email: string, password: string) => Promise<void>;
  signUp: (email: string, password: string) => Promise<void>;
  signOut: (notice?: Notice) => Promise<void>;
  /** Parola değişince sunucunun verdiği yeni token'a geç. */
  replaceToken: (token: string) => Promise<void>;
};

const AuthContext = createContext<AuthContextValue | null>(null);

export function AuthProvider({ children }: { children: ReactNode }) {
  const queryClient = useQueryClient();
  const [state, setState] = useState<AuthState>({ status: 'restoring' });
  const [notice, setNotice] = useState<Notice | null>(null);
  const tokenRef = useRef<string | null>(null);

  const signOut = useCallback(
    async (next?: Notice) => {
      tokenRef.current = null;
      setState({ status: 'signedOut' });
      setNotice(next ?? null);
      queryClient.clear();
      await storage.removeItem(TOKEN_KEY);
    },
    [queryClient],
  );

  // API istemcisini oturuma bağla. Etkiler bildirim sırasıyla çalıştığı için oturum geri yüklenmeden önce olur.
  useEffect(() => {
    configureApi({
      baseUrl: appConfig.apiBaseUrl,
      showBaseUrlInErrors: __DEV__,
      getToken: () => tokenRef.current,
      onUnauthorized: () => {
        if (tokenRef.current) void signOut(notices.sessionEnded);
      },
    });
  }, [signOut]);

  /** Token'ı doğrular, hesabı yükler ve sürücü değilse reddeder. */
  const establishSession = useCallback(async (token: string): Promise<Account> => {
    tokenRef.current = token;
    try {
      const account = await accountApi.get();
      if (!isDriver(account)) throw new Error(notices.notDriver.message);
      return account;
    } catch (error) {
      tokenRef.current = null;
      throw error;
    }
  }, []);

  // Uygulama açılışında kayıtlı oturumu geri yükle.
  useEffect(() => {
    let cancelled = false;

    (async () => {
      const token = await storage.getItem(TOKEN_KEY);
      if (cancelled) return;

      if (!token) {
        setState({ status: 'signedOut' });
        return;
      }
      if (isJwtExpired(token)) {
        await storage.removeItem(TOKEN_KEY);
        setNotice(notices.sessionExpired);
        setState({ status: 'signedOut' });
        return;
      }

      try {
        const account = await establishSession(token);
        if (!cancelled) setState({ status: 'signedIn', token, account });
      } catch (error) {
        // 401'de API istemcisi oturumu zaten kapattı (onUnauthorized).
        if (cancelled || isApiStatus(error, 401)) return;
        if (error instanceof Error && error.message === notices.notDriver.message) {
          await signOut(notices.notDriver);
          return;
        }
        // Çevrimdışı açılış: token geçerli görünüyorsa son bilinen bilgilerle devam et.
        const offlineAccount = accountFromToken(token);
        if (offlineAccount) {
          tokenRef.current = token;
          setState({ status: 'signedIn', token, account: offlineAccount });
        } else {
          await signOut();
        }
      }
    })();

    return () => {
      cancelled = true;
    };
  }, [establishSession, signOut]);

  // Token süresi dolunca oturumu kapat (API 60 dakikalık token veriyor, yenileme ucu yok).
  useEffect(() => {
    if (state.status !== 'signedIn') return;
    const token = state.token;
    const check = () => {
      if (isJwtExpired(token, Date.now(), 0)) void signOut(notices.sessionExpired);
    };
    check();
    const interval = setInterval(check, 30_000);
    const subscription = AppState.addEventListener('change', (status) => {
      if (status === 'active') check();
    });
    return () => {
      clearInterval(interval);
      subscription.remove();
    };
  }, [state, signOut]);

  const signIn = useCallback(
    async (email: string, password: string) => {
      let token: string;
      try {
        ({ token } = await authApi.login(email.trim(), password));
      } catch (error) {
        if (isApiStatus(error, 401)) throw new Error(INVALID_CREDENTIALS_MESSAGE);
        throw error;
      }
      const account = await establishSession(token);
      await storage.setItem(TOKEN_KEY, token);
      setNotice(null);
      setState({ status: 'signedIn', token, account });
    },
    [establishSession],
  );

  const signUp = useCallback(
    async (email: string, password: string) => {
      await authApi.register(email.trim(), password);
      await signIn(email, password);
    },
    [signIn],
  );

  const replaceToken = useCallback(async (token: string) => {
    tokenRef.current = token;
    await storage.setItem(TOKEN_KEY, token);
    setState((previous) => (previous.status === 'signedIn' ? { ...previous, token } : previous));
  }, []);

  const clearNotice = useCallback(() => setNotice(null), []);

  const value = useMemo<AuthContextValue>(
    () => ({ state, notice, clearNotice, signIn, signUp, signOut, replaceToken }),
    [state, notice, clearNotice, signIn, signUp, signOut, replaceToken],
  );

  return <AuthContext value={value}>{children}</AuthContext>;
}

export function useAuth(): AuthContextValue {
  const context = use(AuthContext);
  if (!context) throw new Error('useAuth, AuthProvider içinde kullanılmalı.');
  return context;
}

/** Oturum açık ekranlarda hesap bilgisi. */
export function useAccount(): Account {
  const { state } = useAuth();
  if (state.status !== 'signedIn') throw new Error('Bu ekran oturum açıkken kullanılabilir.');
  return state.account;
}
