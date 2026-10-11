import { colors } from './tokens';

/**
 * Başlık çubuğu panelin `navbar-dark bg-dark` çubuğu gibi: koyu zemin, beyaz başlık, solda marka.
 */
export const headerOptions = {
  headerStyle: { backgroundColor: colors.navbar },
  headerTintColor: colors.navbarText,
  headerTitleStyle: { fontWeight: '500' as const },
  headerTitleAlign: 'left' as const,
  headerShadowVisible: false,
};
