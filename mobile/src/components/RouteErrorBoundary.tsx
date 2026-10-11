import type { ErrorBoundaryProps } from 'expo-router';

import { AlertBox } from './AlertBox';
import { Button } from './Button';
import { Screen } from './Screen';

/** Ekranlardan biri çökerse: panelin hata uyarısı görünümünde mesaj ve yeniden deneme. */
export function RouteErrorBoundary({ error, retry }: ErrorBoundaryProps) {
  return (
    <Screen edges={['top', 'bottom']}>
      <AlertBox tone="danger" title="Beklenmeyen bir hata oluştu">
        {error.message || 'Ekran yüklenemedi.'}
      </AlertBox>
      <Button title="Tekrar dene" onPress={() => void retry()} />
    </Screen>
  );
}
