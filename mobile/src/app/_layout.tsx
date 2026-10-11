import { QueryClientProvider } from '@tanstack/react-query';
import { Stack, type ErrorBoundaryProps } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect } from 'react';
import { SafeAreaProvider } from 'react-native-safe-area-context';

import { AuthProvider, useAuth } from '@/auth/AuthProvider';
import { BrandSplash } from '@/components/BrandSplash';
import { RouteErrorBoundary } from '@/components/RouteErrorBoundary';
import { queryClient, subscribeAppFocus } from '@/lib/queryClient';
import { ReservationProvider } from '@/ride/ReservationProvider';
import { colors } from '@/theme';
import { headerOptions } from '@/theme/navigation';

export function ErrorBoundary(props: ErrorBoundaryProps) {
  return <RouteErrorBoundary {...props} />;
}

export default function RootLayout() {
  useEffect(() => subscribeAppFocus(), []);

  return (
    <SafeAreaProvider>
      <QueryClientProvider client={queryClient}>
        <AuthProvider>
          <ReservationProvider>
            {/* Başlıklar koyu (navbar) olduğu için durum çubuğu açık renkli. */}
            <StatusBar style="light" />
            <RootNavigator />
          </ReservationProvider>
        </AuthProvider>
      </QueryClientProvider>
    </SafeAreaProvider>
  );
}

function RootNavigator() {
  const { state } = useAuth();

  if (state.status === 'restoring') return <BrandSplash />;

  const signedIn = state.status === 'signedIn';

  return (
    <Stack
      screenOptions={{
        ...headerOptions,
        headerBackButtonDisplayMode: 'minimal',
        contentStyle: { backgroundColor: colors.bodyBg },
      }}>
      <Stack.Protected guard={!signedIn}>
        <Stack.Screen name="sign-in" options={{ headerShown: false }} />
        <Stack.Screen name="sign-up" options={{ title: 'Kayıt Ol' }} />
        <Stack.Screen name="server" options={{ title: 'Sunucu Ayarları', presentation: 'modal' }} />
      </Stack.Protected>

      <Stack.Protected guard={signedIn}>
        <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
        <Stack.Screen name="ride/[id]" options={{ title: 'Sürüş Özeti' }} />
        <Stack.Screen name="change-password" options={{ title: 'Parolayı Değiştir', presentation: 'modal' }} />
        <Stack.Screen name="delete-account" options={{ title: 'Hesabı Sil', presentation: 'modal' }} />
      </Stack.Protected>

      <Stack.Screen name="+not-found" options={{ title: 'Bulunamadı' }} />
    </Stack>
  );
}
