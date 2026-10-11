import { useRouter } from 'expo-router';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Linking, StyleSheet, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { useActiveRide, useServiceAreas, useVehicle, useVehiclesInBounds } from '@/api/queries';
import type { Vehicle } from '@/api/types';
import { AlertBox } from '@/components/AlertBox';
import { MapIconButton } from '@/components/MapIconButton';
import { NearbyCard } from '@/features/map/NearbyCard';
import { ReservationCard } from '@/features/map/ReservationCard';
import { RideInProgressCard } from '@/features/map/RideInProgressCard';
import { VehicleCard } from '@/features/map/VehicleCard';
import { VehicleMap, type VehicleMapHandle } from '@/features/map/VehicleMap';
import { useUserLocation } from '@/hooks/useUserLocation';
import { DEFAULT_REGION, isInBounds, regionToBounds, type MapRegion } from '@/lib/geo';
import { useReservation } from '@/ride/ReservationProvider';
import { colors, spacing } from '@/theme';

export default function MapScreen() {
  const router = useRouter();
  const mapRef = useRef<VehicleMapHandle>(null);
  const centeredOnUser = useRef(false);

  const { permission, location, refresh: refreshLocation } = useUserLocation();
  const [region, setRegion] = useState<MapRegion>(DEFAULT_REGION);
  const [selected, setSelected] = useState<Vehicle | null>(null);
  const [panelHeight, setPanelHeight] = useState(0);
  const [vanishedNotice, setVanishedNotice] = useState(false);

  const activeRideQuery = useActiveRide();
  const activeRide = activeRideQuery.data ?? null;
  const { reservation, expired, dismissExpired } = useReservation();
  const reservedVehicle = useVehicle(reservation?.vehicleId).data ?? null;
  const busy = Boolean(activeRide || reservation);

  const bounds = useMemo(() => regionToBounds(region), [region]);
  const vehiclesQuery = useVehiclesInBounds(bounds, !activeRide);
  const serviceAreas = useServiceAreas().data ?? [];
  const available = useMemo(() => vehiclesQuery.data?.items ?? [], [vehiclesQuery.data]);

  // Konum ilk geldiğinde haritayı bir kez sürücünün üstüne getir.
  useEffect(() => {
    if (location && !centeredOnUser.current) {
      centeredOnUser.current = true;
      mapRef.current?.centerOn(location);
    }
  }, [location]);

  // Seçim, render sırasında türetilen koşullara göre bırakılır (efekt yerine React'in önerdiği kalıp):
  // - rezervasyon ya da sürüş başlayınca seçim kartı yerini onlara bırakır;
  // - seçili araç görünen alandaysa ama yenilenen listede yoksa başkası rezerve etmiştir.
  const vehiclesSettled = vehiclesQuery.isSuccess && !vehiclesQuery.isFetching && !vehiclesQuery.isPlaceholderData;
  if (selected && busy) {
    setSelected(null);
  } else if (
    selected &&
    vehiclesSettled &&
    isInBounds(selected, bounds) &&
    !available.some((vehicle) => vehicle.id === selected.id)
  ) {
    setSelected(null);
    setVanishedNotice(true);
  }

  // Seçili aracın en güncel bilgisi; harita başka yere kaydırılsa da kart açık kalır.
  const selectedVehicle = selected ? (available.find((vehicle) => vehicle.id === selected.id) ?? selected) : null;

  // Sürüşte diğer araçlar gizlenir; rezervasyonda sürücünün aracı "Rezerve" rengiyle görünür.
  const markers: Vehicle[] = useMemo(() => {
    if (activeRide) return [];
    const others = available.filter((vehicle) => vehicle.id !== reservation?.vehicleId);
    return reservation && reservedVehicle ? [reservedVehicle, ...others] : others;
  }, [activeRide, available, reservation, reservedVehicle]);

  const onSelect = (vehicleId: string | null) => {
    if (busy) return;
    setVanishedNotice(false);
    setSelected(vehicleId ? (available.find((vehicle) => vehicle.id === vehicleId) ?? null) : null);
  };

  const centerOnUser = async () => {
    const current = location ?? (await refreshLocation(true));
    if (current) mapRef.current?.centerOn(current);
  };

  let panel;
  if (activeRide) {
    panel = <RideInProgressCard ride={activeRide} onCompleted={(rideId) => router.push(`/ride/${rideId}`)} />;
  } else if (reservation) {
    panel = <ReservationCard reservation={reservation} vehicle={reservedVehicle} userLocation={location} />;
  } else if (selectedVehicle) {
    panel = <VehicleCard vehicle={selectedVehicle} userLocation={location} onClose={() => setSelected(null)} />;
  } else {
    panel = (
      <NearbyCard
        loading={vehiclesQuery.isPending}
        shown={available.length}
        total={vehiclesQuery.data?.totalCount ?? 0}
      />
    );
  }

  return (
    <View style={styles.root}>
      <VehicleMap
        ref={mapRef}
        vehicles={markers}
        serviceAreas={serviceAreas}
        selectedId={selectedVehicle?.id ?? null}
        initialRegion={DEFAULT_REGION}
        showsUserLocation={permission === 'granted'}
        bottomInset={panelHeight}
        onSelect={onSelect}
        onRegionChange={setRegion}
      />

      <View style={styles.fabs}>
        <MapIconButton icon="locate" label="Konumuma git" onPress={centerOnUser} />
        <MapIconButton
          icon="refresh"
          label="Araçları yenile"
          onPress={() => {
            void vehiclesQuery.refetch();
            void activeRideQuery.refetch();
          }}
        />
      </View>

      <View style={styles.panel} onLayout={(event) => setPanelHeight(event.nativeEvent.layout.height)}>
        {permission === 'denied' ? (
          <AlertBox tone="warning" action={{ label: 'Ayarları aç', onPress: () => void Linking.openSettings() }}>
            Konum izni yok. Yakındaki araçları görmek ve sürüşü bitirdiğin yeri kaydetmek için konum gerekir.
          </AlertBox>
        ) : null}
        {expired ? (
          <AlertBox tone="warning" onDismiss={dismissExpired}>
            Rezervasyon süresi doldu; araç yeniden müsait.
          </AlertBox>
        ) : null}
        {vanishedNotice ? (
          <AlertBox tone="info" onDismiss={() => setVanishedNotice(false)}>
            Seçtiğin araç artık müsait değil.
          </AlertBox>
        ) : null}
        {vehiclesQuery.isError && !activeRide ? (
          <AlertBox tone="danger" action={{ label: 'Tekrar dene', onPress: () => void vehiclesQuery.refetch() }}>
            {errorMessage(vehiclesQuery.error)}
          </AlertBox>
        ) : null}
        {activeRideQuery.isError ? (
          <AlertBox tone="danger" action={{ label: 'Tekrar dene', onPress: () => void activeRideQuery.refetch() }}>
            {errorMessage(activeRideQuery.error)}
          </AlertBox>
        ) : null}
        {panel}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: colors.tertiaryBg,
  },
  fabs: {
    position: 'absolute',
    top: spacing[3],
    right: spacing[3],
    gap: spacing[2],
  },
  panel: {
    position: 'absolute',
    left: 12,
    right: 12,
    bottom: 12,
    gap: spacing[2],
  },
});
