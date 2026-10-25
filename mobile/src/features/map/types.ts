import type { ServiceArea, Vehicle } from '@/api/types';
import type { LatLng, MapRegion } from '@/lib/geo';
import type { Scheme } from '@/theme';

export type VehicleMapHandle = {
  /** Noktayı, alttaki panelin üstünde kalan alanın ortasına getirir. */
  centerOn: (point: LatLng, zoom?: number) => void;
};

export type VehicleMapProps = {
  /** Haritada gösterilecek araçlar (müsaitler ve sürücünün rezerve ettiği araç). */
  vehicles: Vehicle[];
  serviceAreas: ServiceArea[];
  selectedId: string | null;
  initialRegion: MapRegion;
  /** Sürücünün konumu (mavi nokta); izin yoksa null. */
  userLocation: LatLng | null;
  /** Haritanın altını kaplayan panelin yüksekliği; bildirilen alan ve odak bunun üstünde kalır. */
  bottomInset: number;
  /** Açık ya da koyu harita */
  scheme: Scheme;
  onSelect: (vehicleId: string | null) => void;
  /** Görünen alan değişince (panelin altında kalan kısım hariç). */
  onRegionChange: (region: MapRegion) => void;
};
