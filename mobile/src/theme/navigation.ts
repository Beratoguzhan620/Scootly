import type { Theme } from './palette';
import { fontFamily } from './typography';

/** Yığın ekranlarının başlık çubuğu: ekran zeminiyle bütün, gölgesiz, lacivert başlık. */
export function stackHeaderOptions(theme: Theme) {
  return {
    headerStyle: { backgroundColor: theme.bg },
    headerTintColor: theme.text,
    headerTitleStyle: { fontFamily: fontFamily.semibold, fontSize: 18, color: theme.text },
    headerTitleAlign: 'center' as const,
    headerShadowVisible: false,
    headerBackButtonDisplayMode: 'minimal' as const,
    contentStyle: { backgroundColor: theme.bg },
  };
}
