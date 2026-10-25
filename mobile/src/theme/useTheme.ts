import { StyleSheet, useColorScheme } from 'react-native';

import { themes, type Theme } from './palette';

/** Telefonun açık/koyu tema ayarını izler. */
export function useTheme(): Theme {
  return useColorScheme() === 'dark' ? themes.dark : themes.light;
}

/**
 * Temaya bağlı stil sayfası. Her tema için bir kez oluşturulur ve önbellekte tutulur:
 *
 *     const useStyles = makeStyles((t) => ({ root: { backgroundColor: t.bg } }));
 *     const styles = useStyles();
 */
export function makeStyles<T extends StyleSheet.NamedStyles<T>>(factory: (theme: Theme) => T) {
  const cache = new Map<Theme, T>();
  return function useStyles(): T {
    const theme = useTheme();
    let styles = cache.get(theme);
    if (!styles) {
      styles = StyleSheet.create(factory(theme));
      cache.set(theme, styles);
    }
    return styles;
  };
}
