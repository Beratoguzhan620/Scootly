import { Stack } from 'expo-router';

import { useTheme } from '@/theme';
import { stackHeaderOptions } from '@/theme/navigation';

/** Filo yönetimi: sekmeler ve üstlerine açılan araç ekranları. */
export default function FleetLayout() {
  const theme = useTheme();
  return (
    <Stack screenOptions={stackHeaderOptions(theme)}>
      <Stack.Screen name="(tabs)" options={{ headerShown: false }} />
      <Stack.Screen name="vehicle/[id]" options={{ title: 'Araç' }} />
      <Stack.Screen name="new-vehicle" options={{ title: 'Yeni araç', presentation: 'modal' }} />
    </Stack>
  );
}
