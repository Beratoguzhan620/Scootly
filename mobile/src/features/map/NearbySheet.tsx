import { ActivityIndicator, View } from 'react-native';

import { AppText } from '@/components/AppText';
import { BottomSheet, type SheetFrameProps } from '@/components/BottomSheet';
import { Button } from '@/components/Button';
import { makeStyles, space, textVariants, useTheme } from '@/theme';

type Props = SheetFrameProps & {
  loading: boolean;
  /** Haritada gösterilen araç sayısı */
  shown: number;
  /** Görünen alandaki müsait araçların toplamı */
  total: number;
  /** Kiralanabilecek en az bir araç var mı (en yakını göster düğmesi için) */
  canShowNearest: boolean;
  onShowNearest: () => void;
};

/** Seçim yokken: görünen alandaki scooter sayısı ve en yakınına gitme. */
export function NearbySheet({ loading, shown, total, canShowNearest, onShowNearest, ...frame }: Props) {
  const theme = useTheme();
  const styles = useStyles();

  let header;
  if (loading) {
    header = (
      <View style={styles.row} accessibilityLiveRegion="polite">
        <ActivityIndicator color={theme.text} />
        <AppText variant="heading">Scooter&apos;lar aranıyor…</AppText>
      </View>
    );
  } else if (total === 0) {
    header = (
      <View style={styles.texts}>
        <AppText variant="title">Bu alanda müsait scooter yok</AppText>
        <AppText muted>Haritayı kaydırarak ya da uzaklaştırarak çevreye bak.</AppText>
      </View>
    );
  } else {
    header = (
      <View style={styles.row} accessible accessibilityLabel={`Bu alanda ${total} müsait scooter var`}>
        <AppText style={styles.count}>{total}</AppText>
        <View style={styles.texts}>
          <AppText variant="heading">müsait scooter</AppText>
          <AppText muted variant="caption">
            Ayrıntılar için haritada birine dokun.
          </AppText>
        </View>
      </View>
    );
  }

  const showNearest = !loading && total > 0 && canShowNearest;
  const partial = !loading && total > shown;

  return (
    <BottomSheet {...frame} header={header} accessibilityLabel="Yakındaki scooter'lar">
      {showNearest || partial ? (
        <>
          {showNearest ? <Button title="En yakın scooter'ı göster" icon="navigate" onPress={onShowNearest} /> : null}
          {partial ? (
            <AppText variant="caption" muted align="center">
              {`Haritada ${total} scooter'dan ${shown} tanesi gösteriliyor. Daha fazlası için yakınlaştır.`}
            </AppText>
          ) : null}
        </>
      ) : null}
    </BottomSheet>
  );
}

const useStyles = makeStyles(() => ({
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md + 2,
  },
  texts: {
    flex: 1,
    gap: 2,
  },
  count: {
    ...textVariants.numeral,
    fontSize: 44,
    lineHeight: 46,
  },
}));
