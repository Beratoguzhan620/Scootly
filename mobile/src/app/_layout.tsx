import { QueryClientProvider } from '@tanstack/react-query';
import { useFonts } from 'expo-font';
import { Stack, type ErrorBoundaryProps } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import * as SystemUI from 'expo-system-ui';
import { useEffect } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { AuthProvider, useAuth } from '@/auth/AuthProvider';
import { BrandSplash } from '@/components/BrandSplash';
import { RouteErrorBoundary } from '@/components/RouteErrorBoundary';
import { queryClient, subscribeAppFocus } from '@/lib/queryClient';
import { PaymentProvider } from '@/payments/PaymentProvider';
import { ReservationProvider } from '@/ride/ReservationProvider';
import { useTheme } from '@/theme';
import { fontAssets } from '@/theme/fonts';
import { stackHeaderOptions } from '@/theme/navigation';

export function ErrorBoundary(props: ErrorBoundaryProps) {
  return <RouteErrorBoundary {...props} />;
}

export default function RootLayout() {
  useEffect(() => subscribeAppFocus(), []);
  // Yazı tipi yüklenemezse (bozuk dosya vb.) sistem yazısıyla devam edilir.
  const [fontsLoaded, fontError] = useFonts(fontAssets);

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <PaymentProvider>
            <ReservationProvider>
              <RootNavigator ready={fontsLoaded || Boolean(fontError)} />
            </ReservationProvider>
          </PaymentProvider>
        </AuthProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}

function RootNavigator({ ready }: { ready: boolean }) {
  const { state } = useAuth();
  const theme = useTheme();

  // Klavye açılırken ya da ekran geçişlerinde kenarlarda beyaz şerit görünmesin.
  useEffect(() => {
    void SystemUI.setBackgroundColorAsync(theme.bg).catch(() => undefined);
  }, [theme.bg]);

  if (!ready || state.status === 'restoring') return <BrandSplash />;

  const signedIn = state.status === 'signedIn';
  const driver = signedIn && state.mode === 'driver';
  const fleet = signedIn && state.mode === 'fleet';

  return (
    <>
      <StatusBar style={theme.statusBar} />
      <Stack screenOptions={stackHeaderOptions(theme)}>
        <Stack.Protected guard={!signedIn}>
          <Stack.Screen name="sign-in" options={{ headerShown: false }} />
          <Stack.Screen name="sign-up" options={{ title: 'Kayıt ol' }} />
          <Stack.Screen name="server" options={{ title: 'Sunucu ayarları', presentation: 'modal' }} />
        </Stack.Protected>

        <Stack.Protected guard={driver}>
          <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
          <Stack.Screen name="ride/[id]" options={{ title: 'Sürüş ve ödeme' }} />
          <Stack.Screen name="scan" options={{ headerShown: false, presentation: 'fullScreenModal' }} />
          <Stack.Screen name="start-ride" options={{ title: 'Sürüşe başla' }} />
          <Stack.Screen name="payment-method" options={{ title: 'Ödeme yöntemi', presentation: 'modal' }} />
        </Stack.Protected>

        <Stack.Protected guard={fleet}>
          <Stack.Screen name="fleet" options={{ headerShown: false }} />
        </Stack.Protected>

        <Stack.Protected guard={signedIn}>
          <Stack.Screen name="vehicle/[id]" options={{ title: 'Scooter' }} />
          <Stack.Screen name="change-password" options={{ title: 'Parolayı değiştir', presentation: 'modal' }} />
          <Stack.Screen name="delete-account" options={{ title: 'Hesabı sil', presentation: 'modal' }} />
        </Stack.Protected>

        <Stack.Screen name="+not-found" options={{ title: 'Bulunamadı' }} />
      </Stack>
    </>
  );
}
