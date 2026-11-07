import { useMemo } from 'react';
import Svg, { Path, Rect } from 'react-native-svg';

import { qrMatrix } from '@/lib/qr';
import { INK } from '@/theme';

type Props = {
  value: string;
  size?: number;
  /** Ekran okuyucu için ne olduğu */
  accessibilityLabel: string;
};

/** Koyu temada da okunsun diye her zaman beyaz zeminde lacivert; çevresinde 4 modüllük sessiz alan. */
export function QrCode({ value, size = 220, accessibilityLabel }: Props) {
  const matrix = useMemo(() => qrMatrix(value), [value]);
  const quiet = 4;
  const total = matrix.size + quiet * 2;
  // Modüller tam piksele otursun: satırlar arasında ince çizgi kalmaz, okuma kolaylaşır.
  const pixels = Math.max(1, Math.floor(size / total)) * total;

  return (
    <Svg
      width={pixels}
      height={pixels}
      viewBox={`${-quiet} ${-quiet} ${total} ${total}`}
      accessible
      accessibilityRole="image"
      accessibilityLabel={accessibilityLabel}>
      <Rect x={-quiet} y={-quiet} width={total} height={total} fill="#FFFFFF" />
      <Path d={matrix.path} fill={INK} />
    </Svg>
  );
}
