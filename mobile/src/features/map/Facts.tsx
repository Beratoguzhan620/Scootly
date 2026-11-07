import { Children, Fragment, isValidElement, type ReactNode } from 'react';
import { Text, View } from 'react-native';

import { Icon, type IconName } from '@/components/Icon';
import { makeStyles, radius, space, textVariants, useTheme } from '@/theme';

/** Aracın kısa bilgileri yan yana: menzil, mesafe, yürüme süresi. */
export function FactStrip({ children }: { children: ReactNode }) {
  const styles = useStyles();
  const items = Children.toArray(children).filter(isValidElement);
  return (
    <View style={styles.strip}>
      {items.map((item, index) => (
        <Fragment key={item.key ?? index}>
          {index > 0 ? <View style={styles.divider} /> : null}
          {item}
        </Fragment>
      ))}
    </View>
  );
}

export function Fact({ icon, value, label }: { icon: IconName; value: string; label: string }) {
  const theme = useTheme();
  const styles = useStyles();
  return (
    <View style={styles.fact} accessible accessibilityLabel={`${label}: ${value}`}>
      <Icon name={icon} size={18} color={theme.textMuted} />
      <Text style={styles.value} numberOfLines={1}>
        {value}
      </Text>
      <Text style={styles.label} numberOfLines={1}>
        {label}
      </Text>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  strip: {
    flexDirection: 'row',
    alignItems: 'stretch',
    backgroundColor: t.surfaceAlt,
    borderRadius: radius.control + 2,
    paddingVertical: space.md,
  },
  divider: {
    width: 1,
    backgroundColor: t.line,
    marginVertical: space.xs,
  },
  fact: {
    flex: 1,
    alignItems: 'center',
    gap: 2,
    paddingHorizontal: space.sm,
  },
  value: {
    ...textVariants.bodyStrong,
    fontSize: 17,
    color: t.text,
    marginTop: space.xs,
  },
  label: {
    ...textVariants.caption,
    color: t.textMuted,
  },
}));
