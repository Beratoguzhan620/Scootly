import { useRouter } from 'expo-router';
import { useMemo } from 'react';
import { ActivityIndicator, RefreshControl, SectionList, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { errorMessage } from '@/api/client';
import { useActiveRide, useRideHistory } from '@/api/queries';
import type { Ride } from '@/api/types';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { LargeTitle } from '@/components/Screen';
import { EmptyState, LoadingView } from '@/components/StateViews';
import { RideRow, RideRowSeparator } from '@/features/rides/RideRow';
import { useNow } from '@/hooks/useNow';
import { formatMonthYear, formatTime, monthKey, parseServerDate } from '@/lib/format';
import { makeStyles, screenPadding, space, useTheme } from '@/theme';

type Section = { key: string; title: string; data: Ride[] };

/** Sürüşleri başladıkları aya göre gruplar (liste zaten yeniden eskiye sıralı gelir). */
function groupByMonth(rides: Ride[]): Section[] {
  const sections: Section[] = [];
  for (const ride of rides) {
    const date = parseServerDate(ride.startedAt);
    const key = monthKey(date);
    let section = sections[sections.length - 1];
    if (!section || section.key !== key) {
      section = { key, title: formatMonthYear(date), data: [] };
      sections.push(section);
    }
    section.data.push(ride);
  }
  return sections;
}

export default function RidesScreen() {
  const theme = useTheme();
  const styles = useStyles();
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const history = useRideHistory();
  const activeRide = useActiveRide().data ?? null;
  const now = useNow(60_000);
  const rides = useMemo(() => history.data?.pages.flatMap((page) => page.items) ?? [], [history.data]);
  const sections = useMemo(() => groupByMonth(rides), [rides]);

  const header = (
    <View style={styles.header}>
      <LargeTitle>Sürüşlerim</LargeTitle>
      {activeRide ? (
        <Notice tone="brand" action={{ label: 'Haritaya dön', onPress: () => router.navigate('/') }}>
          {`Devam eden bir sürüşün var (başlangıç ${formatTime(parseServerDate(activeRide.startedAt))}).`}
        </Notice>
      ) : null}
      {history.isError && rides.length > 0 ? (
        <Notice tone="danger" action={{ label: 'Tekrar dene', onPress: () => void history.refetch() }}>
          {errorMessage(history.error)}
        </Notice>
      ) : null}
    </View>
  );

  const empty = history.isPending ? (
    <LoadingView message="Sürüşler yükleniyor…" />
  ) : history.isError ? (
    <Notice tone="danger" action={{ label: 'Tekrar dene', onPress: () => void history.refetch() }}>
      {errorMessage(history.error)}
    </Notice>
  ) : (
    <EmptyState
      icon="navigate"
      title="Henüz sürüşün yok"
      message="Haritadan bir scooter seçip rezerve et; sürüşlerin ve ödemelerin burada listelenir."
      action={<Button title="Haritaya git" icon="map" inline onPress={() => router.navigate('/')} />}
    />
  );

  return (
    <SectionList
      style={styles.list}
      contentContainerStyle={[styles.content, { paddingTop: insets.top + space.lg }]}
      sections={sections}
      keyExtractor={(ride) => ride.id}
      stickySectionHeadersEnabled={false}
      ListHeaderComponent={header}
      ListEmptyComponent={empty}
      renderSectionHeader={({ section }) => (
        <AppText variant="label" muted style={styles.sectionTitle} accessibilityRole="header">
          {section.title}
        </AppText>
      )}
      renderSectionFooter={() => <View style={styles.sectionGap} />}
      ItemSeparatorComponent={RideRowSeparator}
      renderItem={({ item, index, section }) => (
        <RideRow
          ride={item}
          first={index === 0}
          last={index === section.data.length - 1}
          now={now}
          onPress={() => router.push(`/ride/${item.id}`)}
        />
      )}
      onEndReachedThreshold={0.4}
      onEndReached={() => {
        if (history.hasNextPage && !history.isFetchingNextPage) void history.fetchNextPage();
      }}
      ListFooterComponent={
        history.isFetchingNextPage ? <ActivityIndicator style={styles.footer} color={theme.text} /> : null
      }
      refreshControl={
        <RefreshControl
          refreshing={history.isRefetching && !history.isFetchingNextPage}
          onRefresh={() => void history.refetch()}
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
    gap: space.lg,
    marginBottom: space.lg,
  },
  sectionTitle: {
    marginBottom: space.sm,
    marginLeft: space.xs,
  },
  sectionGap: {
    height: space.xl,
  },
  footer: {
    marginVertical: space.lg,
  },
}));
