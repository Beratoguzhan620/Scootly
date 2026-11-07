import { useLocalSearchParams, useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useMemo, useRef, useState } from 'react';
import { ActivityIndicator, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { errorMessage } from '@/api/client';
import { useFleetInBounds, useServiceAreas } from '@/api/queries';
import type { VehicleStatus } from '@/api/types';
import { useAccount } from '@/auth/AuthProvider';
import { isFleetManager } from '@/auth/account';
import { AppText } from '@/components/AppText';
import { BottomSheet, type SheetFrameProps } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { MapButton } from '@/components/MapButton';
import { Notice } from '@/components/Notice';
import { Wordmark } from '@/components/Wordmark';
import { countByStatus } from '@/domain/fleet';
import { vehicleStatusMeta } from '@/domain/labels';
import { FleetVehicleSheet } from '@/features/fleet/FleetVehicleSheet';
import { StatusFilter } from '@/features/fleet/StatusFilter';
import { FOCUS_ZOOM } from '@/features/map/mapConfig';
import { VehicleMap, type VehicleMapHandle } from '@/features/map/VehicleMap';
import { useUserLocation } from '@/hooks/useUserLocation';
import { DEFAULT_REGION, regionToBounds, type MapRegion } from '@/lib/geo';
import { haptics } from '@/lib/haptics';
import { makeStyles, radius, space, textVariants, useTheme } from '@/theme';

/** Filo haritası: görünen alandaki bütün araçlar, durum renkleriyle. */
export default function FleetMapScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const account = useAccount();
  const manager = isFleetManager(account);
  const mapRef = useRef<VehicleMapHandle>(null);
  const centeredOnUser = useRef(false);
  /** Araç sayfasındaki "Haritada göster" */
  const { focus, lat, lng } = useLocalSearchParams<{ focus?: string; lat?: string; lng?: string }>();

  const { location, refresh: refreshLocation } = useUserLocation();
  const [region, setRegion] = useState<MapRegion>(DEFAULT_REGION);
  const [filter, setFilter] = useState<VehicleStatus | null>(null);
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [sheetHeight, setSheetHeight] = useState(0);

  const bounds = useMemo(() => regionToBounds(region), [region]);
  const query = useFleetInBounds(bounds);
  const serviceAreas = useServiceAreas().data ?? [];
  const vehicles = useMemo(() => query.data?.items ?? [], [query.data]);
  const counts = useMemo(() => countByStatus(vehicles), [vehicles]);
  const shown = useMemo(
    () => (filter ? vehicles.filter((vehicle) => vehicle.status === filter) : vehicles),
    [vehicles, filter],
  );
  const selected = selectedId ? (vehicles.find((vehicle) => vehicle.id === selectedId) ?? null) : null;

  useEffect(() => {
    if (location && !centeredOnUser.current && !focus) {
      centeredOnUser.current = true;
      mapRef.current?.centerOn(location);
    }
  }, [location, focus]);

  // Odaklanılan araç seçilir ve süzgeç kaldırılır (render sırasında, React'in "prop değişince state" kalıbı).
  const [focusedId, setFocusedId] = useState<string | null>(null);
  if ((focus ?? null) !== focusedId) {
    setFocusedId(focus ?? null);
    if (focus) {
      setFilter(null);
      setSelectedId(focus);
    }
  }

  useEffect(() => {
    if (!focus || !lat || !lng) return;
    centeredOnUser.current = true;
    mapRef.current?.centerOn({ latitude: Number(lat), longitude: Number(lng) }, FOCUS_ZOOM + 1);
    router.setParams({ focus: undefined, lat: undefined, lng: undefined });
  }, [focus, lat, lng, router]);

  const onSelect = (vehicleId: string | null) => {
    if (vehicleId && vehicleId !== selectedId) haptics.select();
    setSelectedId(vehicleId);
  };

  const centerOnUser = async () => {
    const current = location ?? (await refreshLocation(true));
    if (current) mapRef.current?.centerOn(current);
  };

  const frame: SheetFrameProps = {
    onVisibleHeightChange: setSheetHeight,
    notices: query.isError ? (
      <Notice tone="danger" action={{ label: 'Tekrar dene', onPress: () => void query.refetch() }}>
        {errorMessage(query.error)}
      </Notice>
    ) : null,
    accessory: (
      <>
        <MapButton icon="locate" label="Konumuma git" onPress={() => void centerOnUser()} />
        <MapButton
          icon="refresh"
          label="Araçları yenile"
          busy={query.isFetching && !query.isPending}
          onPress={() => void query.refetch()}
        />
      </>
    ),
  };

  const partial = (query.data?.totalCount ?? 0) > vehicles.length;

  return (
    <View style={styles.root}>
      <StatusBar style={theme.statusBar} />
      <VehicleMap
        ref={mapRef}
        vehicles={shown}
        serviceAreas={serviceAreas}
        selectedId={selected?.id ?? null}
        initialRegion={DEFAULT_REGION}
        userLocation={location}
        bottomInset={sheetHeight}
        scheme={theme.scheme}
        statusPins
        onSelect={onSelect}
        onRegionChange={setRegion}
      />

      <View style={[styles.top, { paddingTop: insets.top + space.md }]} pointerEvents="box-none">
        <View style={styles.brandRow} pointerEvents="none">
          <View style={styles.brand}>
            <Wordmark size={22} />
          </View>
          <View style={styles.mode}>
            <Text style={styles.modeText}>Filo</Text>
          </View>
        </View>
        <StatusFilter counts={counts} value={filter} onChange={setFilter} floating />
      </View>

      {selected ? (
        <FleetVehicleSheet
          key={`vehicle-${selected.id}`}
          {...frame}
          vehicle={selected}
          userLocation={location}
          onClose={() => setSelectedId(null)}
        />
      ) : (
        <BottomSheet
          key="summary"
          {...frame}
          accessibilityLabel="Filo özeti"
          header={
            query.isPending ? (
              <View style={styles.summaryRow} accessibilityLiveRegion="polite">
                <ActivityIndicator color={theme.text} />
                <AppText variant="heading">Araçlar yükleniyor…</AppText>
              </View>
            ) : (
              <View
                style={styles.summaryRow}
                accessible
                accessibilityLabel={
                  filter
                    ? `Görünen alanda ${shown.length} araç ${vehicleStatusMeta(filter).label.toLocaleLowerCase('tr-TR')}`
                    : `Görünen alanda ${counts.total} araç; ${counts.Maintenance} bakımda, ${counts.Lost} kayıp`
                }>
                <Text style={styles.count}>{filter ? shown.length : counts.total}</Text>
                <View style={styles.summaryText}>
                  <AppText variant="heading">
                    {filter ? `${vehicleStatusMeta(filter).label.toLocaleLowerCase('tr-TR')} araç` : 'araç bu alanda'}
                  </AppText>
                  <AppText variant="caption" muted>
                    {filter
                      ? "Bu alanda. Hepsini görmek için Tümü'ne dokun."
                      : counts.Maintenance + counts.Lost > 0
                        ? `${counts.Maintenance} bakımda, ${counts.Lost} kayıp. Ayrıntı için işarete dokun.`
                        : 'Ayrıntı ve işlemler için bir işarete dokun.'}
                  </AppText>
                </View>
              </View>
            )
          }>
          <View style={styles.actions}>
            {manager ? (
              <Button
                title="Araç ekle"
                icon="add"
                style={styles.action}
                onPress={() =>
                  router.push({
                    pathname: '/fleet/new-vehicle',
                    params: { lat: String(region.latitude), lng: String(region.longitude) },
                  })
                }
              />
            ) : null}
            <Button
              title="Araç listesi"
              variant="secondary"
              icon="list"
              style={styles.action}
              onPress={() => router.navigate('/fleet/vehicles')}
            />
          </View>
          {partial ? (
            <AppText variant="caption" muted align="center">
              {`Bu alandaki ${query.data?.totalCount} aracın ${vehicles.length} tanesi gösteriliyor. Daha fazlası için yakınlaştır.`}
            </AppText>
          ) : null}
        </BottomSheet>
      )}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  root: {
    flex: 1,
    overflow: 'hidden',
    backgroundColor: t.bg,
  },
  top: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    gap: space.sm,
  },
  brandRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
    paddingHorizontal: space.lg,
  },
  brand: {
    backgroundColor: t.brand,
    borderRadius: radius.control - 4,
    paddingHorizontal: space.md,
    paddingTop: 1,
    paddingBottom: 3,
    boxShadow: t.floatShadow,
  },
  mode: {
    backgroundColor: t.strong,
    borderRadius: radius.control - 4,
    paddingHorizontal: space.md,
    paddingVertical: space.xs + 1,
    boxShadow: t.floatShadow,
  },
  modeText: {
    ...textVariants.captionStrong,
    fontSize: 14,
    color: t.onStrong,
    letterSpacing: 0.4,
  },
  summaryRow: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md + 2,
  },
  summaryText: {
    flex: 1,
    gap: 2,
  },
  count: {
    ...textVariants.numeral,
    fontSize: 44,
    lineHeight: 46,
    color: t.text,
  },
  actions: {
    flexDirection: 'row',
    gap: space.sm,
  },
  action: {
    flex: 1,
    paddingHorizontal: space.md,
  },
}));
