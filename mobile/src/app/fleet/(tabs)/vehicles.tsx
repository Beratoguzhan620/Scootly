import { useRouter } from 'expo-router';
import { useMemo, useState } from 'react';
import { FlatList, RefreshControl, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { errorMessage } from '@/api/client';
import { useFleetAll } from '@/api/queries';
import type { VehicleStatus } from '@/api/types';
import { useAccount } from '@/auth/AuthProvider';
import { isFleetManager } from '@/auth/account';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { LargeTitle } from '@/components/Screen';
import { EmptyState, LoadingView } from '@/components/StateViews';
import { TextField } from '@/components/TextField';
import { countByStatus, matchesSearch, sortForFleet } from '@/domain/fleet';
import { vehicleStatusMeta } from '@/domain/labels';
import { FleetVehicleRow } from '@/features/fleet/FleetVehicleRow';
import { StatusFilter } from '@/features/fleet/StatusFilter';
import { makeStyles, screenPadding, space, useTheme } from '@/theme';

/** Bütün filo: duruma göre süz, plaka kodu ya da markayla ara; dikkat isteyen araçlar önce. */
export default function FleetVehiclesScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const manager = isFleetManager(useAccount());
  const all = useFleetAll();
  const [filter, setFilter] = useState<VehicleStatus | null>(null);
  const [search, setSearch] = useState('');

  const vehicles = useMemo(() => all.data?.items ?? [], [all.data]);
  const counts = useMemo(() => countByStatus(vehicles), [vehicles]);
  const list = useMemo(
    () =>
      sortForFleet(vehicles.filter((vehicle) => (!filter || vehicle.status === filter) && matchesSearch(vehicle, search))),
    [vehicles, filter, search],
  );
  const truncated = (all.data?.totalCount ?? 0) > vehicles.length;

  const header = (
    <View style={styles.header}>
      <View style={styles.titleRow}>
        <LargeTitle>Araçlar</LargeTitle>
        {manager ? (
          <Button title="Araç ekle" icon="add" inline onPress={() => router.push('/fleet/new-vehicle')} />
        ) : null}
      </View>
      <TextField
        label="Ara"
        value={search}
        onChangeText={setSearch}
        placeholder="Plaka kodu ya da marka"
        autoCapitalize="none"
        autoCorrect={false}
        returnKeyType="search"
        clearButtonMode="while-editing"
      />
      <View style={styles.filter}>
        <StatusFilter counts={counts} value={filter} onChange={setFilter} inset={screenPadding} />
      </View>
      {all.isError && vehicles.length > 0 ? (
        <Notice tone="danger" action={{ label: 'Tekrar dene', onPress: () => void all.refetch() }}>
          {errorMessage(all.error)}
        </Notice>
      ) : null}
      {truncated ? (
        <AppText variant="caption" muted>
          {`Filoda ${all.data?.totalCount} araç var; ilk ${vehicles.length} tanesi listeleniyor.`}
        </AppText>
      ) : null}
    </View>
  );

  const empty = all.isPending ? (
    <LoadingView message="Araçlar yükleniyor…" />
  ) : all.isError ? (
    <Notice tone="danger" action={{ label: 'Tekrar dene', onPress: () => void all.refetch() }}>
      {errorMessage(all.error)}
    </Notice>
  ) : vehicles.length === 0 ? (
    <EmptyState
      icon="add"
      title="Filoda henüz araç yok"
      message={manager ? 'İlk aracı ekle; QR kodunu yazdırıp scooter\'a yapıştırabilirsin.' : 'Filo yöneticisi araç ekleyince burada görünür.'}
      action={
        manager ? <Button title="Araç ekle" icon="add" inline onPress={() => router.push('/fleet/new-vehicle')} /> : undefined
      }
    />
  ) : (
    <EmptyState
      icon="search"
      title="Eşleşen araç yok"
      message={filter ? `"${vehicleStatusMeta(filter).label}" durumunda aramaya uyan araç yok.` : 'Aramayı değiştirip tekrar dene.'}
    />
  );

  return (
    <FlatList
      style={styles.list}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + space.lg }]}
      data={list}
      keyExtractor={(vehicle) => vehicle.id}
      ListHeaderComponent={header}
      ListEmptyComponent={empty}
      keyboardShouldPersistTaps="handled"
      renderItem={({ item, index }) => (
        <FleetVehicleRow
          vehicle={item}
          first={index === 0}
          last={index === list.length - 1}
          onPress={() => router.push({ pathname: '/fleet/vehicle/[id]', params: { id: item.id } })}
        />
      )}
      refreshControl={
        <RefreshControl
          refreshing={all.isRefetching}
          onRefresh={() => void all.refetch()}
          tintColor={theme.text}
          colors={[theme.text]}
          progressBackgroundColor={theme.surface}
        />
      }
    />
  );
}

const useStyles = makeStyles((t) => ({
  list: {
    flex: 1,
    backgroundColor: t.bg,
  },
  content: {
    flexGrow: 1,
    paddingHorizontal: screenPadding,
    paddingBottom: space.xxl,
  },
  header: {
    gap: space.md,
    marginBottom: space.lg,
  },
  titleRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: space.md,
  },
  filter: {
    marginHorizontal: -screenPadding,
  },
}));
