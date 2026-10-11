import Ionicons from '@expo/vector-icons/Ionicons';
import { Pressable, StyleSheet } from 'react-native';

import { colors, shadows } from '@/theme';

type Props = {
  icon: keyof typeof Ionicons.glyphMap;
  label: string;
  onPress: () => void;
};

/** Harita üstündeki yuvarlak `btn-light` düğme. */
export function MapIconButton({ icon, label, onPress }: Props) {
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      hitSlop={6}
      onPress={onPress}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}>
      <Ionicons name={icon} size={22} color={colors.body} />
    </Pressable>
  );
}

const styles = StyleSheet.create({
  button: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: colors.light,
    borderWidth: 1,
    borderColor: colors.border,
    boxShadow: shadows.sm,
  },
  pressed: {
    backgroundColor: '#c6c7c8',
  },
});
