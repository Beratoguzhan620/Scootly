import type { ErrorBoundaryProps } from 'expo-router';

import { Button } from './Button';
import { Notice } from './Notice';
import { Screen } from './Screen';

/** Ekranlardan biri çökerse: ne olduğunu söyleyen mesaj ve yeniden deneme. */
export function RouteErrorBoundary({ error, retry }: ErrorBoundaryProps) {
  return (
    <Screen edges={['top', 'bottom']}>
      <Notice tone="danger" title="Bu ekran açılamadı">
        {error.message || 'Beklenmeyen bir hata oluştu.'}
      </Notice>
      <Button title="Tekrar dene" onPress={() => void retry()} />
    </Screen>
  );
}
