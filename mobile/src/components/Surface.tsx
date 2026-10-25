import type { ReactNode } from 'react';
import { View, type StyleProp, type ViewStyle } from 'react-native';

import { makeStyles, radius, space } from '@/theme';

type Props = {
  children: ReactNode;
  /** İç boşluk; satır grupları kendi boşluğunu verdiği için kapatılabilir. */
  padded?: boolean;
  style?: StyleProp<ViewStyle>;
};

/** Ekran zemininden ayrılan beyaz yüzey. Gölgesiz: ayrım renk farkıyla. */
export function Surface({ children, padded = true, style }: Props) {
  const styles = useStyles();
  return <View style={[styles.surface, padded && styles.padded, style]}>{children}</View>;
}

const useStyles = makeStyles((t) => ({
  surface: {
    backgroundColor: t.surface,
    borderRadius: radius.surface,
    overflow: 'hidden',
  },
  padded: {
    padding: space.lg + 2,
    gap: space.md,
  },
}));
