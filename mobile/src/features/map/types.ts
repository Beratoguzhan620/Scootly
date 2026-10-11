import type { ServiceArea, Vehicle } from '@/api/types';
import type { LatLng, MapRegion } from '@/lib/geo';

export type VehicleMapHandle = {
  /** Noktayı, alttaki kartın üstünde kalan alanın ortasına getirir. */
  centerOn: (point: LatLng) => void;
};

export type VehicleMapProps = {
  /** Haritada gösterilecek araçlar (müsaitler ve sürücünün rezerve ettiği araç). */
  vehicles: Vehicle[];
  serviceAreas: ServiceArea[];
  selectedId: string | null;
  initialRegion: MapRegion;
  /** Sürücünün konumu (mavi nokta); izin yoksa null. */
  userLocation: LatLng | null;
  /** Haritanın altını kaplayan kartın yüksekliği; bildirilen alan ve odak bunun üstünde kalır. */
  bottomInset: number;
  onSelect: (vehicleId: string | null) => void;
  /** Görünen alan değişince (kartın altında kalan kısım hariç). */
  onRegionChange: (region: MapRegion) => void;
};
