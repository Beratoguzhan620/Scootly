import { Image, StyleSheet, View } from 'react-native';

import { VOLT } from '@/theme';

/**
 * Kayıtlı oturum ve yazı tipleri yüklenirken: sistemin açılış ekranıyla aynı sarı zemin ve logo,
 * böylece geçiş fark edilmez. Yazı tipi henüz yüklenmemiş olabileceği için logo bir resim.
 */
export function BrandSplash() {
  return (
    <View style={styles.root} accessible accessibilityLabel="Scootly yükleniyor">
      <Image source={require('../../assets/splash-icon.png')} style={styles.logo} resizeMode="contain" />
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: VOLT,
  },
  // Sistem açılış ekranındaki görselle aynı boy (app.config.ts → imageWidth).
  logo: {
    width: 160,
    height: 160,
  },
});
