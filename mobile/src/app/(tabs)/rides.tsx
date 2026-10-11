import { useRouter } from 'expo-router';
import { ActivityIndicator, FlatList, RefreshControl, StyleSheet, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { useActiveRide, useRideHistory } from '@/api/queries';
import { AlertBox } from '@/components/AlertBox';
import { EmptyState, LoadingView } from '@/components/StateViews';
import { RideRow, RideRowSeparator } from '@/features/rides/RideRow';
import { useNow } from '@/hooks/useNow';
import { formatTime, parseServerDate } from '@/lib/format';
import { colors, spacing } from '@/theme';

export default function RidesScreen() {
  const router = useRouter();
  const history = useRideHistory();
  const activeRide = useActiveRide().data ?? null;
  const now = useNow(60_000, Boolean(activeRide));
  const rides = history.data?.pages.flatMap((page) => page.items) ?? [];

  const header = (
    <View style={styles.header}>
      {activeRide ? (
        <AlertBox tone="primary" action={{ label: 'Haritaya dön', onPress: () => router.navigate('/') }}>
          {`Devam eden bir sürüşün var (başlangıç ${formatTime(parseServerDate(activeRide.startedAt))}).`}
        </AlertBox>
      ) : null}
      {history.isError && rides.length > 0 ? (
        <AlertBox tone="danger" action={{ label: 'Tekrar dene', onPress: () => void history.refetch() }}>
          {errorMessage(history.error)}
        </AlertBox>
      ) : null}
    </View>
  );

  const empty = history.isPending ? (
    <LoadingView message="Sürüşler yükleniyor..." />
  ) : history.isError ? (
    <AlertBox tone="danger" action={{ label: 'Tekrar dene', onPress: () => void history.refetch() }}>
      {errorMessage(history.error)}
    </AlertBox>
  ) : (
    <EmptyState message="Henüz bir sürüşün yok. Haritadan bir scooter seçerek başlayabilirsin." />
  );

  return (
    <FlatList
      style={styles.list}
      contentContainerStyle={styles.content}
      data={rides}
      keyExtractor={(ride) => ride.id}
      ListHeaderComponent={header}
      ListEmptyComponent={empty}
      ItemSeparatorComponent={RideRowSeparator}
      renderItem={({ item, index }) => (
        <RideRow
          ride={item}
          first={index === 0}
          last={index === rides.length - 1}
          now={now}
          onPress={() => router.push(`/ride/${item.id}`)}
        />
      )}
      onEndReachedThreshold={0.4}
      onEndReached={() => {
        if (history.hasNextPage && !history.isFetchingNextPage) void history.fetchNextPage();
      }}
      ListFooterComponent={
        history.isFetchingNextPage ? <ActivityIndicator style={styles.footer} color={colors.primary} /> : null
      }
      refreshControl={
        <RefreshControl
          refreshing={history.isRefetching && !history.isFetchingNextPage}
          onRefresh={() => void history.refetch()}
          tintColor={colors.primary}
        />
      }
    />
  );
}

const styles = StyleSheet.create({
  list: {
    flex: 1,
    backgroundColor: colors.bodyBg,
  },
  content: {
    flexGrow: 1,
    padding: spacing[3],
  },
  header: {
    gap: spacing[3],
    marginBottom: spacing[3],
  },
  footer: {
    marginVertical: spacing[3],
  },
});
