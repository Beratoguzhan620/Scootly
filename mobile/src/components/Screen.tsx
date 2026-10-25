import type { ReactElement, ReactNode } from 'react';
import {
  KeyboardAvoidingView,
  Platform,
  ScrollView,
  View,
  type RefreshControlProps,
  type StyleProp,
  type ViewStyle,
} from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { AppText } from './AppText';
import { makeStyles, screenPadding, space } from '@/theme';

type Props = {
  children: ReactNode;
  /** Sekme ekranlarında başlık çubuğu yerine içerikte büyük başlık */
  title?: string;
  scroll?: boolean;
  /** Başlık çubuğu olmayan ekranlarda üst, sekme çubuğu olmayan ekranlarda alt güvenli alan. */
  edges?: ('top' | 'bottom')[];
  /** Klavye açılınca içeriği yukarı it (formlar). */
  keyboard?: boolean;
  refreshControl?: ReactElement<RefreshControlProps>;
  contentStyle?: StyleProp<ViewStyle>;
};

export function Screen({
  children,
  title,
  scroll = true,
  edges = [],
  keyboard = false,
  refreshControl,
  contentStyle,
}: Props) {
  const insets = useSafeAreaInsets();
  const styles = useStyles();
  const padding = {
    paddingTop: space.lg + (edges.includes('top') ? insets.top : 0),
    paddingBottom: space.xxl + (edges.includes('bottom') ? insets.bottom : 0),
  };
  const body = (
    <>
      {title ? <LargeTitle>{title}</LargeTitle> : null}
      {children}
    </>
  );

  const content = scroll ? (
    <ScrollView
      style={styles.fill}
      contentContainerStyle={[styles.content, padding, contentStyle]}
      keyboardShouldPersistTaps="handled"
      refreshControl={refreshControl}>
      {body}
    </ScrollView>
  ) : (
    <View style={[styles.fill, styles.content, padding, contentStyle]}>{body}</View>
  );

  if (!keyboard) return <View style={styles.root}>{content}</View>;

  return (
    <KeyboardAvoidingView style={styles.root} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      {content}
    </KeyboardAvoidingView>
  );
}

/** Sekme ekranlarının büyük başlığı (dar, kalın; tabela gibi). */
export function LargeTitle({ children }: { children: string }) {
  const styles = useStyles();
  return (
    <AppText variant="largeTitle" style={styles.largeTitle}>
      {children}
    </AppText>
  );
}

const useStyles = makeStyles((t) => ({
  root: {
    flex: 1,
    backgroundColor: t.bg,
  },
  fill: {
    flex: 1,
  },
  content: {
    flexGrow: 1,
    paddingHorizontal: screenPadding,
    gap: space.lg,
  },
  largeTitle: {
    marginTop: space.sm,
    marginBottom: space.xs,
  },
}));
