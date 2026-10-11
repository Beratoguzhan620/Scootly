import { Link } from 'expo-router';
import { StyleSheet } from 'react-native';

import { AppText } from '@/components/AppText';
import { Screen } from '@/components/Screen';
import { colors } from '@/theme';

export default function NotFoundScreen() {
  return (
    <Screen edges={['bottom']}>
      <AppText variant="h1">Sayfa bulunamadı</AppText>
      <AppText muted>Aradığın ekran yok ya da taşınmış.</AppText>
      <Link href="/" style={styles.link}>
        Ana ekrana dön
      </Link>
    </Screen>
  );
}

const styles = StyleSheet.create({
  link: {
    color: colors.link,
    textDecorationLine: 'underline',
    fontSize: 16,
  },
});
