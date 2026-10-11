import type { ReactElement, ReactNode } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  StyleSheet,
  View,
  type RefreshControlProps,
  type StyleProp,
  type ViewStyle,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { colors, spacing } from '@/theme';

type Props = {
  children: ReactNode;
  scroll?: boolean;
  /** Başlık çubuğu olmayan ekranlarda üst, sekme çubuğu olmayan ekranlarda alt güvenli alan. */
  edges?: ('top' | 'bottom')[];
  /** Klavye açılınca içeriği yukarı it (formlar). */
  keyboard?: boolean;
  refreshControl?: ReactElement<RefreshControlProps>;
  contentStyle?: StyleProp<ViewStyle>;
};

/** Panelin `.container` karşılığı: beyaz zemin, 16 birim yatay boşluk. */
export function Screen({ children, scroll = true, edges = [], keyboard = false, refreshControl, contentStyle }: Props) {
  const insets = useSafeAreaInsets();
  const padding = {
    paddingTop: spacing[3] + (edges.includes('top') ? insets.top : 0),
    paddingBottom: spacing[3] + (edges.includes('bottom') ? insets.bottom : 0),
  };

  const content = scroll ? (
    <ScrollView
      style={styles.fill}
      contentContainerStyle={[styles.content, padding, contentStyle]}
      keyboardShouldPersistTaps="handled"
      refreshControl={refreshControl}>
      {children}
    </ScrollView>
  ) : (
    <View style={[styles.fill, styles.content, padding, contentStyle]}>{children}</View>
  );

  if (!keyboard) return <View style={styles.root}>{content}</View>;

  return (
    <KeyboardAvoidingView style={styles.root} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      {content}
    </KeyboardAvoidingView>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: colors.bodyBg,
  },
  fill: {
    flex: 1,
  },
  content: {
    flexGrow: 1,
    paddingHorizontal: spacing[3],
    gap: spacing[3],
  },
});
