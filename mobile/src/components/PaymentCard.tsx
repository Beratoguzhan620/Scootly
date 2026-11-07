import { StyleSheet, Text, View } from 'react-native';

import { cardBrandLabels, type CardBrand } from '@/domain/card';
import { fontFamily, INK, radius, space, textVariants, VOLT } from '@/theme';

type Props = {
  brand: CardBrand;
  /** Gösterilecek son haneler (yazarken eksik olabilir) */
  last4: string;
  holder: string;
  /** "08/29" */
  expiry: string;
};

/** Kartın görünümü: lacivert zemin, sarı çip; numaranın yalnızca son 4 hanesi görünür. */
export function PaymentCard({ brand, last4, holder, expiry }: Props) {
  const masked = `•••• •••• •••• ${last4.padEnd(4, '•')}`;
  return (
    <View
      style={styles.card}
      accessible
      accessibilityLabel={`${cardBrandLabels[brand]} kartı, son dört hane ${last4.split('').join(' ') || 'yok'}`}>
      <View style={styles.top}>
        <View style={styles.chip} />
        <Text style={styles.brand}>{brand === 'unknown' ? 'Kart' : cardBrandLabels[brand]}</Text>
      </View>
      <Text style={styles.number} numberOfLines={1}>
        {masked}
      </Text>
      <View style={styles.bottom}>
        <View style={styles.field}>
          <Text style={styles.caption}>Kart sahibi</Text>
          <Text style={styles.value} numberOfLines={1}>
            {holder || 'Ad Soyad'}
          </Text>
        </View>
        <View style={styles.expiry}>
          <Text style={styles.caption}>Son kullanma</Text>
          <Text style={styles.value}>{expiry || 'AA/YY'}</Text>
        </View>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    aspectRatio: 1.586,
    borderRadius: radius.surface,
    backgroundColor: INK,
    padding: space.xl,
    justifyContent: 'space-between',
    overflow: 'hidden',
  },
  top: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
  },
  chip: {
    width: 42,
    height: 30,
    borderRadius: 6,
    backgroundColor: VOLT,
  },
  brand: {
    fontFamily: fontFamily.wordmark,
    fontSize: 24,
    lineHeight: 28,
    color: '#FFFFFF',
  },
  number: {
    fontFamily: fontFamily.condensedSemibold,
    fontSize: 24,
    lineHeight: 30,
    letterSpacing: 2,
    color: '#FFFFFF',
    fontVariant: ['tabular-nums'],
  },
  bottom: {
    flexDirection: 'row',
    gap: space.lg,
  },
  field: {
    flex: 1,
    gap: 2,
  },
  expiry: {
    gap: 2,
    alignItems: 'flex-end',
  },
  caption: {
    ...textVariants.caption,
    color: '#AEB9D3',
  },
  value: {
    ...textVariants.bodyStrong,
    color: '#FFFFFF',
  },
});
