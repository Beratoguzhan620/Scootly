import { Redirect, useLocalSearchParams } from 'expo-router';

import { useAppMode } from '@/auth/AuthProvider';

/**
 * Scooter'ın QR kodundaki bağlantı (`scootly://vehicle/<id>`). Telefonun kendi kamerasıyla okutulunca uygulama
 * burada açılır: sürücü ödeme ve başlatma ekranına, filo ekibi aracın sayfasına gider.
 */
export default function VehicleLinkScreen() {
  const { id } = useLocalSearchParams<{ id: string }>();
  const { mode } = useAppMode();
  if (mode === 'fleet') return <Redirect href={{ pathname: '/fleet/vehicle/[id]', params: { id } }} />;
  return <Redirect href={{ pathname: '/start-ride', params: { vehicleId: id } }} />;
}
