import type { ServiceArea, Vehicle } from '@/api/types';
import type { LatLng, MapRegion } from '@/lib/geo';

export type VehicleMapHandle = {
  centerOn: (point: LatLng) => void;
};

export type VehicleMapProps = {
  /** Haritada gösterilecek araçlar (müsaitler ve sürücünün rezerve ettiği araç). */
  vehicles: Vehicle[];
  serviceAreas: ServiceArea[];
  selectedId: string | null;
  initialRegion: MapRegion;
  showsUserLocation: boolean;
  /** Haritanın altını kaplayan kartın yüksekliği; harita logosu ve odak bunun üstünde kalır. */
  bottomInset: number;
  onSelect: (vehicleId: string | null) => void;
  onRegionChange: (region: MapRegion) => void;
};
