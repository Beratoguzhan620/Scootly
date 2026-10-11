import { ActivityIndicator, StyleSheet, View } from 'react-native';

import { AppText } from '@/components/AppText';
import { Card } from '@/components/Card';
import { colors, shadows, spacing } from '@/theme';

type Props = {
  loading: boolean;
  shown: number;
  total: number;
};

/** Araç seçilmemişken: görünen alandaki müsait araç sayısı. */
export function NearbyCard({ loading, shown, total }: Props) {
  return (
    <Card style={styles.card} bodyStyle={styles.body}>
      <AppText variant="h5">Yakındaki scooter&apos;lar</AppText>
      {loading ? (
        <View style={styles.row}>
          <ActivityIndicator color={colors.primary} />
          <AppText muted>Araçlar yükleniyor...</AppText>
        </View>
      ) : total === 0 ? (
        <AppText>Bu alanda müsait araç yok. Haritayı kaydırarak çevreye bakabilirsin.</AppText>
      ) : (
        <AppText>
          Bu alanda <AppText weight="700">{total}</AppText> müsait araç var.
        </AppText>
      )}
      {!loading && total > shown ? (
        <AppText variant="small" muted>
          {`${total} araçtan ${shown} tanesi gösteriliyor; daha fazlası için yakınlaştır.`}
        </AppText>
      ) : null}
      {!loading && total > 0 ? (
        <AppText variant="small" muted>
          Ayrıntılar için bir scooter&apos;a dokun.
        </AppText>
      ) : null}
    </Card>
  );
}

const styles = StyleSheet.create({
  card: {
    boxShadow: shadows.md,
  },
  body: {
    gap: spacing[1],
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing[2],
  },
});
