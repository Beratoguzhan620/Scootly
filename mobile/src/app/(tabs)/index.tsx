import { useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useMemo, useRef, useState } from 'react';
import { Linking, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { errorMessage } from '@/api/client';
import { useActiveRide, useServiceAreas, useVehicle, useVehiclesInBounds } from '@/api/queries';
import type { Vehicle } from '@/api/types';
import type { SheetFrameProps } from '@/components/BottomSheet';
import { MapButton } from '@/components/MapButton';
import { Notice } from '@/components/Notice';
import { Wordmark } from '@/components/Wordmark';
import { FOCUS_ZOOM } from '@/features/map/mapConfig';
import { NearbySheet } from '@/features/map/NearbySheet';
import { nearestRentable } from '@/features/map/nearest';
import { ReservationSheet } from '@/features/map/ReservationSheet';
import { RideSheet } from '@/features/map/RideSheet';
import { VehicleMap, type VehicleMapHandle } from '@/features/map/VehicleMap';
import { VehicleSheet } from '@/features/map/VehicleSheet';
import { useUserLocation } from '@/hooks/useUserLocation';
import { DEFAULT_REGION, isInBounds, regionToBounds, type MapRegion } from '@/lib/geo';
import { haptics } from '@/lib/haptics';
import { useReservation } from '@/ride/ReservationProvider';
import { makeStyles, radius, space, useTheme } from '@/theme';

export default function MapScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const mapRef = useRef<VehicleMapHandle>(null);
  const centeredOnUser = useRef(false);

  const { permission, location, refresh: refreshLocation } = useUserLocation();
  const [region, setRegion] = useState<MapRegion>(DEFAULT_REGION);
  const [selected, setSelected] = useState<Vehicle | null>(null);
  const [sheetHeight, setSheetHeight] = useState(0);
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
  // - rezervasyon ya da sürüş başlayınca seçim paneli yerini onlara bırakır;
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

  // Seçili aracın en güncel bilgisi; harita başka yere kaydırılsa da panel açık kalır.
  const selectedVehicle = selected ? (available.find((vehicle) => vehicle.id === selected.id) ?? selected) : null;

  // Sürüşte diğer araçlar gizlenir; rezervasyonda sürücünün aracı seçili gibi vurgulanır.
  const markers: Vehicle[] = useMemo(() => {
    if (activeRide) return [];
    const others = available.filter((vehicle) => vehicle.id !== reservation?.vehicleId);
    return reservation && reservedVehicle ? [reservedVehicle, ...others] : others;
  }, [activeRide, available, reservation, reservedVehicle]);

  const onSelect = (vehicleId: string | null) => {
    if (busy) return;
    setVanishedNotice(false);
    const next = vehicleId ? (available.find((vehicle) => vehicle.id === vehicleId) ?? null) : null;
    if (next && next.id !== selected?.id) haptics.select();
    setSelected(next);
  };

  // Konum yoksa haritanın ortasına en yakın olan.
  const nearest = useMemo(
    () => nearestRentable(available, location ?? { latitude: region.latitude, longitude: region.longitude }),
    [available, location, region.latitude, region.longitude],
  );

  const showNearest = () => {
    if (!nearest) return;
    haptics.select();
    setVanishedNotice(false);
    setSelected(nearest);
    mapRef.current?.centerOn(nearest, FOCUS_ZOOM + 1);
  };

  const centerOnUser = async () => {
    const current = location ?? (await refreshLocation(true));
    if (current) mapRef.current?.centerOn(current);
  };

  const notices = [
    permission === 'denied' ? (
      <Notice
        key="permission"
        tone="warning"
        action={{ label: 'Ayarları aç', onPress: () => void Linking.openSettings() }}>
        Konum izni kapalı. Yakındaki scooter&apos;ları görmek ve sürüşü bitirdiğin yeri kaydetmek için konum gerekir.
      </Notice>
    ) : null,
    expired ? (
      <Notice key="expired" tone="warning" onDismiss={dismissExpired}>
        Rezervasyonun süresi doldu; scooter yeniden müsait.
      </Notice>
    ) : null,
    vanishedNotice ? (
      <Notice key="vanished" tone="info" onDismiss={() => setVanishedNotice(false)}>
        Seçtiğin scooter artık müsait değil.
      </Notice>
    ) : null,
    vehiclesQuery.isError && !activeRide ? (
      <Notice
        key="vehicles"
        tone="danger"
        action={{ label: 'Tekrar dene', onPress: () => void vehiclesQuery.refetch() }}>
        {errorMessage(vehiclesQuery.error)}
      </Notice>
    ) : null,
    activeRideQuery.isError ? (
      <Notice key="ride" tone="danger" action={{ label: 'Tekrar dene', onPress: () => void activeRideQuery.refetch() }}>
        {errorMessage(activeRideQuery.error)}
      </Notice>
    ) : null,
  ].filter(Boolean);

  const frame: SheetFrameProps = {
    onVisibleHeightChange: setSheetHeight,
    notices: notices.length > 0 ? <View style={styles.notices}>{notices}</View> : null,
    accessory: (
      <>
        <MapButton icon="locate" label="Konumuma git" onPress={() => void centerOnUser()} />
        {!activeRide ? (
          <MapButton
            icon="refresh"
            label="Scooter'ları yenile"
            busy={vehiclesQuery.isFetching && !vehiclesQuery.isPending}
            onPress={() => {
              void vehiclesQuery.refetch();
              void activeRideQuery.refetch();
            }}
          />
        ) : null}
      </>
    ),
  };

  let sheet;
  if (activeRide) {
    sheet = (
      <RideSheet key="ride" {...frame} ride={activeRide} onCompleted={(rideId) => router.push(`/ride/${rideId}`)} />
    );
  } else if (reservation) {
    sheet = (
      <ReservationSheet
        key="reservation"
        {...frame}
        reservation={reservation}
        vehicle={reservedVehicle}
        userLocation={location}
      />
    );
  } else if (selectedVehicle) {
    sheet = (
      <VehicleSheet
        key="vehicle"
        {...frame}
        vehicle={selectedVehicle}
        userLocation={location}
        onClose={() => setSelected(null)}
      />
    );
  } else {
    sheet = (
      <NearbySheet
        key="nearby"
        {...frame}
        loading={vehiclesQuery.isPending}
        shown={available.length}
        total={vehiclesQuery.data?.totalCount ?? 0}
        canShowNearest={nearest !== null}
        onShowNearest={showNearest}
      />
    );
  }

  return (
    <View style={styles.root}>
      <StatusBar style={theme.statusBar} />
      <VehicleMap
        ref={mapRef}
        vehicles={markers}
        serviceAreas={serviceAreas}
        // Rezerve edilen araç da seçili gibi vurgulanır.
        selectedId={reservation?.vehicleId ?? selectedVehicle?.id ?? null}
        initialRegion={DEFAULT_REGION}
        userLocation={permission === 'granted' ? location : null}
        bottomInset={sheetHeight}
        scheme={theme.scheme}
        onSelect={onSelect}
        onRegionChange={setRegion}
      />

      <View style={[styles.brand, { top: insets.top + space.md }]} pointerEvents="none">
        <Wordmark size={24} />
      </View>

      {sheet}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  root: {
    flex: 1,
    // Panel küçülünce alt kısmı sekme çubuğunun üstüne taşmasın.
    overflow: 'hidden',
    backgroundColor: t.bg,
  },
  brand: {
    position: 'absolute',
    left: space.lg,
    backgroundColor: t.brand,
    borderRadius: radius.control - 4,
    paddingHorizontal: space.md,
    paddingTop: 1,
    paddingBottom: 3,
    boxShadow: t.floatShadow,
  },
  notices: {
    gap: space.sm,
  },
}));
