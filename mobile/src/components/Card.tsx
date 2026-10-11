import type { ReactNode } from 'react';
import { StyleSheet, Text, View, type StyleProp, type ViewStyle } from 'react-native';

import { colors, radius, spacing, typography } from '@/theme';

type Props = {
  /** `.card-header` metni ya da öğesi */
  header?: ReactNode;
  /** `.card-title` (h5) */
  title?: string;
  children?: ReactNode;
  style?: StyleProp<ViewStyle>;
  bodyStyle?: StyleProp<ViewStyle>;
  /** İçerik kendi dolgusunu yönetecekse (ör. `list-group-flush`). */
  flush?: boolean;
};

export function Card({ header, title, children, style, bodyStyle, flush = false }: Props) {
  return (
    <View style={[styles.card, style]}>
      {header !== undefined ? (
        <View style={styles.header}>
          {typeof header === 'string' ? <Text style={styles.headerText}>{header}</Text> : header}
        </View>
      ) : null}
      <View style={[!flush && styles.body, bodyStyle]}>
        {title ? (
          <Text accessibilityRole="header" style={styles.title}>
            {title}
          </Text>
        ) : null}
        {children}
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  card: {
    backgroundColor: colors.bodyBg,
    borderWidth: 1,
    borderColor: colors.borderTranslucent,
    borderRadius: radius.md,
    overflow: 'hidden',
  },
  header: {
    paddingVertical: spacing[2],
    paddingHorizontal: spacing[3],
    backgroundColor: colors.cardCap,
    borderBottomWidth: 1,
    borderBottomColor: colors.borderTranslucent,
  },
  headerText: {
    ...typography.body,
    color: colors.body,
  },
  body: {
    padding: spacing[3],
  },
  title: {
    ...typography.h5,
    color: colors.body,
    marginBottom: spacing[2],
  },
});
