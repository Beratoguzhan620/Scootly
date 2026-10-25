import { ActivityIndicator, Pressable } from 'react-native';

import { Icon, type IconName } from './Icon';
import { makeStyles, useTheme } from '@/theme';

type Props = {
  icon: IconName;
  label: string;
  onPress: () => void;
  busy?: boolean;
};

/** Harita üstünde yüzen yuvarlak düğme. */
export function MapButton({ icon, label, onPress, busy = false }: Props) {
  const theme = useTheme();
  const styles = useStyles();
  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityState={{ busy }}
      hitSlop={4}
      onPress={onPress}
      style={({ pressed }) => [styles.button, pressed && styles.pressed]}>
      {busy ? <ActivityIndicator color={theme.text} /> : <Icon name={icon} size={22} color={theme.text} />}
    </Pressable>
  );
}

const useStyles = makeStyles((t) => ({
  button: {
    width: 48,
    height: 48,
    borderRadius: 24,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.surface,
    boxShadow: t.floatShadow,
  },
  pressed: {
    backgroundColor: t.surfaceAlt,
    transform: [{ scale: 0.96 }],
  },
}));
