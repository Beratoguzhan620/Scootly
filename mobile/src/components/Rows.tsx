import { Children, Fragment, isValidElement, type ReactNode } from 'react';
import { Pressable, Text, View } from 'react-native';

import { Icon, type IconName } from './Icon';
import { Surface } from './Surface';
import { makeStyles, space, textVariants, useTheme } from '@/theme';

/** Satırları tek yüzeyde, aralarında ince çizgiyle toplar. */
export function RowGroup({ children }: { children: ReactNode }) {
  const styles = useStyles();
  const rows = Children.toArray(children).filter(isValidElement);
  return (
    <Surface padded={false}>
      {rows.map((row, index) => (
        <Fragment key={row.key ?? index}>
          {index > 0 ? <View style={styles.separator} /> : null}
          {row}
        </Fragment>
      ))}
    </Surface>
  );
}

type ListRowProps = {
  title: string;
  icon?: IconName;
  /** Başlığın altında küçük açıklama */
  subtitle?: string;
  onPress?: () => void;
  destructive?: boolean;
  accessibilityHint?: string;
};

/** Dokunulabilir ayar satırı: simge, başlık, sağda ok. */
export function ListRow({ title, icon, subtitle, onPress, destructive = false, accessibilityHint }: ListRowProps) {
  const theme = useTheme();
  const styles = useStyles();
  const color = destructive ? theme.critical.fg : theme.text;

  return (
    <Pressable
      accessibilityRole="button"
      accessibilityLabel={title}
      accessibilityHint={accessibilityHint}
      onPress={onPress}
      style={({ pressed }) => [styles.row, pressed && styles.pressed]}>
      {icon ? (
        <View style={[styles.iconBox, destructive && { backgroundColor: theme.critical.bg }]}>
          <Icon name={icon} size={19} color={color} />
        </View>
      ) : null}
      <View style={styles.texts}>
        <Text style={[styles.title, { color }]}>{title}</Text>
        {subtitle ? (
          <Text style={styles.subtitle} numberOfLines={2}>
            {subtitle}
          </Text>
        ) : null}
      </View>
      {onPress ? <Icon name="chevron-forward" size={18} color={theme.textSubtle} /> : null}
    </Pressable>
  );
}

/** Etiket–değer satırı (sürüş ayrıntıları gibi). */
export function InfoRow({ label, value }: { label: string; value: ReactNode }) {
  const styles = useStyles();
  return (
    <View style={styles.infoRow}>
      <Text style={styles.infoLabel}>{label}</Text>
      {typeof value === 'string' ? <Text style={styles.infoValue}>{value}</Text> : value}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  separator: {
    height: 1,
    backgroundColor: t.line,
    marginLeft: space.lg + 2,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md + 2,
    minHeight: 60,
    paddingHorizontal: space.lg + 2,
    paddingVertical: space.md,
  },
  pressed: {
    backgroundColor: t.surfaceAlt,
  },
  iconBox: {
    width: 34,
    height: 34,
    borderRadius: 10,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.surfaceAlt,
  },
  texts: {
    flex: 1,
    gap: 2,
  },
  title: {
    ...textVariants.bodyStrong,
  },
  subtitle: {
    ...textVariants.caption,
    color: t.textMuted,
  },
  infoRow: {
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    gap: space.lg,
    minHeight: 52,
    paddingHorizontal: space.lg + 2,
    paddingVertical: space.sm + 2,
  },
  infoLabel: {
    ...textVariants.body,
    color: t.textMuted,
  },
  infoValue: {
    ...textVariants.bodyStrong,
    color: t.text,
    flexShrink: 1,
    textAlign: 'right',
  },
}));
