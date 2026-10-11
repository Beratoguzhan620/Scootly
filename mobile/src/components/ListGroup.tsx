import { Children, Fragment, type ReactNode } from 'react';
import { Pressable, StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';

import { colors, radius, spacing, typography } from '@/theme';

type GroupProps = {
  children: ReactNode;
  /** `.list-group-flush` — kart içinde dış kenarlık olmadan */
  flush?: boolean;
  style?: StyleProp<ViewStyle>;
};

/** Bootstrap `.list-group`; öğeler arasına ince ayırıcı çizgi koyar. */
export function ListGroup({ children, flush = false, style }: GroupProps) {
  const items = Children.toArray(children).filter(Boolean);
  return (
    <View style={[!flush && styles.group, style]}>
      {items.map((child, index) => (
        <Fragment key={index}>
          {index > 0 ? <ListDivider /> : null}
          {child}
        </Fragment>
      ))}
    </View>
  );
}

export function ListDivider() {
  return <View style={styles.divider} />;
}

type ItemProps = {
  children: ReactNode;
  onPress?: () => void;
  accessibilityLabel?: string;
  style?: StyleProp<ViewStyle>;
};

/** `.list-group-item` (dokunulabiliyorsa `.list-group-item-action`). */
export function ListGroupItem({ children, onPress, accessibilityLabel, style }: ItemProps) {
  if (!onPress) return <View style={[styles.item, style]}>{children}</View>;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={accessibilityLabel}
      onPress={onPress}
      style={({ pressed }) => [styles.item, pressed && styles.itemPressed, style]}>
      {children}
    </Pressable>
  );
}

/** Paneldeki `list-group-item d-flex justify-content-between` satırı. */
export function KeyValueRow({ label, value }: { label: string; value: ReactNode }) {
  return (
    <View style={styles.row}>
      <Text style={styles.key}>{label}</Text>
      {typeof value === 'string' || typeof value === 'number' ? <Text style={styles.value}>{value}</Text> : value}
    </View>
  );
}

const styles = StyleSheet.create({
  group: {
    borderWidth: 1,
    borderColor: colors.borderTranslucent,
    borderRadius: radius.md,
    overflow: 'hidden',
    backgroundColor: colors.bodyBg,
  },
  divider: {
    height: StyleSheet.hairlineWidth,
    backgroundColor: colors.borderTranslucent,
  },
  item: {
    paddingVertical: 12,
    paddingHorizontal: spacing[3],
    backgroundColor: colors.bodyBg,
  },
  itemPressed: {
    backgroundColor: colors.tertiaryBg,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: spacing[3],
    paddingVertical: 10,
  },
  key: {
    ...typography.body,
    color: colors.muted,
  },
  value: {
    ...typography.body,
    color: colors.body,
    flexShrink: 1,
    textAlign: 'right',
  },
});
