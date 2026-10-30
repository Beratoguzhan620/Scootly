import { StyleSheet, Text, View } from 'react-native';

import type { CardBrand } from '@/domain/card';
import { fontFamily, INK, radius, VOLT } from '@/theme';

const LABELS: Record<CardBrand, string> = {
  visa: 'VISA',
  mastercard: 'MC',
  amex: 'AMEX',
  troy: 'TROY',
  unknown: 'KART',
};

/** Kartın küçük resmi: lacivert zeminde markanın kısaltması, köşede sarı çip. Logo değil, yalnızca yazı. */
export function CardBadge({ brand }: { brand: CardBrand | null }) {
  return (
    <View style={styles.card} accessibilityElementsHidden importantForAccessibility="no">
      <View style={styles.chip} />
      <Text style={styles.label} numberOfLines={1}>
        {brand ? LABELS[brand] : '+'}
      </Text>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    width: 52,
    height: 36,
    borderRadius: radius.plate,
    backgroundColor: INK,
    justifyContent: 'flex-end',
    paddingHorizontal: 6,
    paddingBottom: 3,
  },
  chip: {
    position: 'absolute',
    top: 6,
    left: 6,
    width: 11,
    height: 8,
    borderRadius: 2,
    backgroundColor: VOLT,
  },
  label: {
    fontFamily: fontFamily.condensedBold,
    fontSize: 13,
    lineHeight: 15,
    letterSpacing: 0.6,
    color: '#FFFFFF',
    textAlign: 'right',
  },
});
