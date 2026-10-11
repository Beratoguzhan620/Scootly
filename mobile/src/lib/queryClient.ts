import { focusManager, QueryClient } from '@tanstack/react-query';
import { AppState, Platform } from 'react-native';

import { ApiError } from '@/api/client';

export const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 15_000,
      // 4xx (404, 409, 429...) tekrar denemekle düzelmez; ağ ve sunucu hatalarında 2 kez daha denenir.
      retry: (failureCount, error) => {
        if (error instanceof ApiError && error.status !== null && error.status < 500) return false;
        return failureCount < 2;
      },
    },
    mutations: {
      retry: false,
    },
  },
});

/** Uygulama ön plana dönünce bayat verileri yenile (React Native'de pencere odağı yok). */
export function subscribeAppFocus(): () => void {
  if (Platform.OS === 'web') return () => {};
  const subscription = AppState.addEventListener('change', (status) => {
    focusManager.setFocused(status === 'active');
  });
  return () => subscription.remove();
}
