import { useEffect, useImperativeHandle, useRef, useState, type Ref } from 'react';
import { StyleSheet } from 'react-native';
import MapView, { Marker, Polygon } from 'react-native-maps';

import type { VehicleMapHandle, VehicleMapProps } from './types';
import { VehicleMarker } from './VehicleMarker';
import type { Vehicle } from '@/api/types';
import { vehicleStatusMeta } from '@/domain/labels';
import { regionAround } from '@/lib/geo';
import { colors } from '@/theme';

export type { VehicleMapHandle };

/**
 * Android'de özel görünümlü işaretler bir kez "fotoğraflanır". İlk çizimde boş kalmasın diye kısa süre
 * izlenir, sonra performans için bırakılır; içerik değişince `key` ile yeniden oluşturulur.
 */
function VehiclePin({ vehicle, selected, onPress }: { vehicle: Vehicle; selected: boolean; onPress: () => void }) {
  const [tracksViewChanges, setTracksViewChanges] = useState(true);

  useEffect(() => {
    const timer = setTimeout(() => setTracksViewChanges(false), 500);
    return () => clearTimeout(timer);
  }, []);

  return (
    <Marker
      coordinate={{ latitude: vehicle.latitude, longitude: vehicle.longitude }}
      anchor={{ x: 0.5, y: 0.5 }}
      tracksViewChanges={tracksViewChanges}
      zIndex={selected ? 2 : 1}
      accessibilityLabel={`${vehicleStatusMeta(vehicle.status).label} scooter, batarya yüzde ${vehicle.batteryPercentage}`}
      onPress={(event) => {
        event.stopPropagation();
        onPress();
      }}>
      <VehicleMarker status={vehicle.status} battery={vehicle.batteryPercentage} selected={selected} />
    </Marker>
  );
}

export function VehicleMap({
  vehicles,
  serviceAreas,
  selectedId,
  initialRegion,
  showsUserLocation,
  bottomInset,
  onSelect,
  onRegionChange,
  ref,
}: VehicleMapProps & { ref?: Ref<VehicleMapHandle> }) {
  const mapRef = useRef<MapView>(null);

  useImperativeHandle(
    ref,
    () => ({
      centerOn: (point) => mapRef.current?.animateToRegion(regionAround(point), 500),
    }),
    [],
  );

  return (
    <MapView
      ref={mapRef}
      style={StyleSheet.absoluteFill}
      initialRegion={initialRegion}
      showsUserLocation={showsUserLocation}
      showsMyLocationButton={false}
      showsCompass={false}
      toolbarEnabled={false}
      rotateEnabled={false}
      pitchEnabled={false}
      mapPadding={{ top: 0, right: 0, bottom: bottomInset, left: 0 }}
      onRegionChangeComplete={(region) => onRegionChange(region)}
      onPress={(event) => {
        // İşarete dokunmak bazı platformlarda haritaya da dokunma olarak gelir.
        if (event.nativeEvent.action !== 'marker-press') onSelect(null);
      }}>
      {serviceAreas.map((area) => (
        <Polygon
          key={area.name}
          coordinates={area.boundary}
          strokeColor={colors.primary}
          strokeWidth={2}
          fillColor="rgba(13, 110, 253, 0.06)"
          tappable={false}
        />
      ))}
      {vehicles.map((vehicle) => {
        const selected = vehicle.id === selectedId;
        return (
          <VehiclePin
            key={`${vehicle.id}:${vehicle.status}:${vehicle.batteryPercentage}:${selected ? 1 : 0}`}
            vehicle={vehicle}
            selected={selected}
            onPress={() => onSelect(vehicle.id)}
          />
        );
      })}
    </MapView>
  );
}
