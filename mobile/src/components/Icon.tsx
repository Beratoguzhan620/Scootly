import Ionicons from '@expo/vector-icons/Ionicons';
import type { ComponentProps } from 'react';

import { useTheme } from '@/theme';

export type IconName = ComponentProps<typeof Ionicons>['name'];

/** Ionicons; renk verilmezse temanın metin rengi. Süs amaçlı olduğu için ekran okuyucudan gizli. */
export function Icon({ name, size = 20, color }: { name: IconName; size?: number; color?: string }) {
  const theme = useTheme();
  return (
    <Ionicons
      name={name}
      size={size}
      color={color ?? theme.text}
      accessibilityElementsHidden
      importantForAccessibility="no"
    />
  );
}
