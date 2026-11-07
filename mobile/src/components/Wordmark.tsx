import { Text, type StyleProp, type TextStyle } from 'react-native';

import { fontFamily, INK } from '@/theme';

/** "scootly" logosu: dar, kalın, öne eğik — hareket halindeki bir tabela yazısı. */
export function Wordmark({
  size = 28,
  color = INK,
  style,
}: {
  size?: number;
  color?: string;
  style?: StyleProp<TextStyle>;
}) {
  return (
    <Text
      accessibilityRole="header"
      accessibilityLabel="Scootly"
      style={[
        {
          fontFamily: fontFamily.wordmark,
          fontSize: size,
          lineHeight: Math.round(size * 1.05),
          letterSpacing: -size * 0.01,
          color,
        },
        style,
      ]}>
      scootly
    </Text>
  );
}
