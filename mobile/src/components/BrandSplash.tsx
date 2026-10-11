import { ActivityIndicator, StyleSheet, Text, View } from 'react-native';

import { colors, spacing, typography } from '@/theme';

/** Kayıtlı oturum kontrol edilirken: navbar renginde marka ekranı. */
export function BrandSplash() {
  return (
    <View style={styles.root} accessibilityLabel="Scootly yükleniyor">
      <Text style={styles.brand}>Scootly</Text>
      <ActivityIndicator color={colors.navbarLink} />
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: spacing[3],
    backgroundColor: colors.navbar,
  },
  brand: {
    ...typography.h1,
    fontWeight: '400',
    color: colors.navbarText,
  },
});
