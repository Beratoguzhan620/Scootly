import { Text, View } from 'react-native';

import { formatDuration } from '@/lib/format';
import { makeStyles, radius, space, textVariants } from '@/theme';

/** Rezervasyonun kalan süresi: sarı levha üstünde dakika:saniye. */
export function CountdownPlate({ remaining }: { remaining: number }) {
  const styles = useStyles();
  const minutesLeft = Math.ceil(remaining / 60_000);

  return (
    <View
      style={styles.plate}
      accessible
      accessibilityRole="timer"
      accessibilityLabel={`Rezervasyonun bitmesine ${minutesLeft} dakika`}>
      <Text style={styles.value}>{formatDuration(remaining)}</Text>
      <Text style={styles.label}>kaldı</Text>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  plate: {
    alignItems: 'center',
    backgroundColor: t.brand,
    borderRadius: radius.control,
    paddingHorizontal: space.md + 2,
    paddingTop: space.xs + 2,
    paddingBottom: space.xs,
    minWidth: 92,
  },
  value: {
    ...textVariants.numeral,
    fontSize: 30,
    lineHeight: 32,
    color: t.onBrand,
  },
  label: {
    ...textVariants.captionStrong,
    marginTop: -2,
    color: t.onBrand,
  },
}));
