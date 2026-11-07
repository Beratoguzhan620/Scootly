import { StyleSheet, Text, View } from 'react-native';

import { shortId } from '@/lib/format';
import { fontFamily, radius, useTheme } from '@/theme';

type Props = {
  /** Araç ya da sürüş kimliği; ilk 8 karakteri plaka gibi gösterilir. */
  id: string;
  /** Ekran okuyucu için ne olduğu: "Araç", "Sürüş" */
  kind: string;
  /** Lacivert sürüş panelinde açık renkli çerçeve */
  inverse?: boolean;
};

/** Kimlikleri scooter'ların üstündeki numara plakası gibi gösterir. */
export function Plate({ id, kind, inverse = false }: Props) {
  const theme = useTheme();
  const code = shortId(id).toUpperCase();
  const color = inverse ? theme.ride.text : theme.text;
  const border = inverse ? theme.ride.muted : theme.lineStrong;

  return (
    <View
      style={[styles.plate, { borderColor: border }]}
      accessible
      accessibilityLabel={`${kind} kodu ${code.split('').join(' ')}`}>
      <Text style={[styles.code, { color }]}>{code}</Text>
    </View>
  );
}

const styles = StyleSheet.create({
  plate: {
    alignSelf: 'flex-start',
    borderWidth: 1.5,
    borderRadius: radius.plate,
    paddingHorizontal: 7,
    paddingVertical: 1,
  },
  code: {
    fontFamily: fontFamily.condensedSemibold,
    fontSize: 15,
    lineHeight: 20,
    letterSpacing: 1.2,
    fontVariant: ['tabular-nums'],
  },
});
