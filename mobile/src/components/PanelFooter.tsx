import type { ReactNode } from 'react';
import { StyleSheet, Text, View } from 'react-native';

import { colors, spacing, typography } from '@/theme';

/** Panelin alt bilgisi: `border-top footer text-muted` — "© 2026 - Scootly". */
export function PanelFooter({ children }: { children?: ReactNode }) {
  return (
    <View style={styles.footer}>
      <Text style={styles.text}>© {new Date().getFullYear()} - Scootly</Text>
      {children}
    </View>
  );
}

const styles = StyleSheet.create({
  footer: {
    marginTop: 'auto',
    paddingTop: 18,
    borderTopWidth: 1,
    borderTopColor: colors.divider,
    gap: spacing[1],
  },
  text: {
    ...typography.body,
    color: colors.muted,
  },
});
