import { useRouter } from 'expo-router';

import { Button } from '@/components/Button';
import { Screen } from '@/components/Screen';
import { EmptyState } from '@/components/StateViews';

export default function NotFoundScreen() {
  const router = useRouter();
  return (
    <Screen edges={['bottom']}>
      <EmptyState
        icon="compass-outline"
        title="Bu ekran bulunamadı"
        message="Aradığın sayfa yok ya da taşınmış."
        action={<Button title="Ana ekrana dön" inline onPress={() => router.navigate('/')} />}
      />
    </Screen>
  );
}
