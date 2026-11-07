import { useEffect, useRef, useState, type ReactNode } from 'react';
import {
  Animated,
  Platform,
  Pressable,
  StyleSheet,
  View,
  type GestureResponderEvent,
  type LayoutChangeEvent,
  type ViewStyle,
} from 'react-native';

import { useReducedMotion } from '@/hooks/useReducedMotion';
import { haptics } from '@/lib/haptics';
import { makeStyles, radius, screenPadding, space, useTheme } from '@/theme';

/**
 * fixed: sürüklenmez · collapsible: aşağı çekilince yalnızca başlık kalır · dismissible: aşağı çekilince kapanır
 */
export type SheetMode = 'fixed' | 'collapsible' | 'dismissible';

export type SheetFrameProps = {
  /** Panelin hemen üstünde yüzen öğeler (harita düğmeleri); panelle birlikte hareket eder. */
  accessory?: ReactNode;
  /** Başlığın üstündeki uyarılar */
  notices?: ReactNode;
  /** Panelin ekranda görünen yüksekliği değişince (harita bu alanın üstünü kullanır). */
  onVisibleHeightChange?: (height: number) => void;
};

type Props = SheetFrameProps & {
  mode?: SheetMode;
  /** Küçültülünce de görünen kısım: sayaç, ücret, aracın adı */
  header: ReactNode;
  children?: ReactNode;
  onDismiss?: () => void;
  /** Sürüş sırasında ters renkler */
  variant?: 'default' | 'ride';
  /** Ekran okuyucu için panelin adı */
  accessibilityLabel: string;
};

const useNativeDriver = Platform.OS !== 'web';
/** Web önizlemesinde fareyle sürüklerken metin seçilmesin (telefonda zaten seçilmez). */
const noTextSelection = Platform.OS === 'web' ? ({ userSelect: 'none' } as ViewStyle) : null;
const ENTER_OFFSET = 48;
/** Sürüklemenin başladığı sayılması için dikey hareket (px) */
const DRAG_SLOP = 8;

/** Haritanın altındaki panel. Sürüklenebilir; açılışta aşağıdan kayarak gelir. */
export function BottomSheet({
  mode = 'fixed',
  header,
  children,
  notices,
  accessory,
  onDismiss,
  onVisibleHeightChange,
  variant = 'default',
  accessibilityLabel,
}: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const reducedMotion = useReducedMotion();

  const [translateY] = useState(() => new Animated.Value(ENTER_OFFSET));
  const [opacity] = useState(() => new Animated.Value(0));

  const [sheetHeight, setSheetHeight] = useState(0);
  const [headerBottom, setHeaderBottom] = useState(0);
  const [collapsed, setCollapsed] = useState(false);

  // Küçültülünce başlığın altında biraz boşluk kalsın.
  const collapsedHeight = Math.min(sheetHeight, headerBottom + space.lg);
  const collapseOffset = Math.max(0, sheetHeight - collapsedHeight);
  const visibleHeight = collapsed ? collapsedHeight : sheetHeight;
  const draggable = mode !== 'fixed';

  /** Parmağın izi; yalnızca dokunuş işleyicilerinde okunur. */
  const gesture = useRef({ startX: 0, startY: 0, lastY: 0, lastTime: 0, velocity: 0, dragStart: 0, offset: 0 });

  const animateTo = (value: number, onDone?: () => void) => {
    gesture.current.offset = value;
    if (reducedMotion) {
      translateY.setValue(value);
      onDone?.();
      return;
    }
    Animated.spring(translateY, {
      toValue: value,
      useNativeDriver,
      damping: 26,
      stiffness: 260,
      mass: 0.9,
      overshootClamping: value > 0,
    }).start(({ finished }) => {
      if (finished) onDone?.();
    });
  };

  // Açılış: aşağıdan kayarak ve belirerek gelir. İki animasyon ayrı başlatılır; panelin ölçüsü gelince konum
  // animasyonu yenisiyle değişir, belirme yarıda kalmamalı.
  useEffect(() => {
    Animated.spring(translateY, { toValue: 0, useNativeDriver, damping: 24, stiffness: 240 }).start();
    Animated.timing(opacity, { toValue: 1, duration: 160, useNativeDriver }).start();
  }, [translateY, opacity]);

  // Küçültme durumu ya da ölçüler değişince doğru konuma geç.
  useEffect(() => {
    if (sheetHeight === 0) return;
    const target = collapsed ? collapseOffset : 0;
    gesture.current.offset = target;
    if (reducedMotion) {
      translateY.setValue(target);
      return;
    }
    Animated.spring(translateY, {
      toValue: target,
      useNativeDriver,
      damping: 26,
      stiffness: 260,
      mass: 0.9,
      overshootClamping: target > 0,
    }).start();
  }, [collapsed, collapseOffset, sheetHeight, reducedMotion, translateY]);

  useEffect(() => {
    if (sheetHeight > 0) onVisibleHeightChange?.(visibleHeight);
  }, [visibleHeight, sheetHeight, onVisibleHeightChange]);

  const dismiss = () => {
    haptics.select();
    animateTo(sheetHeight + ENTER_OFFSET, () => onDismiss?.());
  };

  const settle = (dy: number, velocity: number) => {
    const offset = gesture.current.dragStart + dy;
    if (mode === 'dismissible') {
      if (offset > sheetHeight * 0.3 || velocity > 0.8) dismiss();
      else animateTo(0);
      return;
    }
    const collapse = velocity > 0.5 ? true : velocity < -0.5 ? false : offset > collapseOffset / 2;
    if (collapse !== collapsed) {
      haptics.select();
      setCollapsed(collapse);
    } else {
      animateTo(collapse ? collapseOffset : 0);
    }
  };

  // Dokunuşlar React Native'in yanıtlayıcı (responder) sistemiyle izlenir: dikey bir sürükleme başlayınca panel
  // dokunuşu düğmelerden devralır, kısa dokunuşlar düğmelere kalır.
  const dragHandlers = draggable
    ? {
        onStartShouldSetResponderCapture: (event: GestureResponderEvent) => {
          const { pageX, pageY, timestamp } = event.nativeEvent;
          Object.assign(gesture.current, {
            startX: pageX,
            startY: pageY,
            lastY: pageY,
            lastTime: timestamp || Date.now(),
            velocity: 0,
          });
          return false;
        },
        onMoveShouldSetResponderCapture: (event: GestureResponderEvent) => {
          const dx = event.nativeEvent.pageX - gesture.current.startX;
          const dy = event.nativeEvent.pageY - gesture.current.startY;
          return Math.abs(dy) > DRAG_SLOP && Math.abs(dy) > Math.abs(dx) * 1.2;
        },
        onResponderGrant: () => {
          gesture.current.dragStart = gesture.current.offset;
        },
        onResponderMove: (event: GestureResponderEvent) => {
          const state = gesture.current;
          const { pageY, timestamp } = event.nativeEvent;
          const time = timestamp || Date.now();
          if (time > state.lastTime) state.velocity = (pageY - state.lastY) / (time - state.lastTime);
          state.lastY = pageY;
          state.lastTime = time;

          const max = mode === 'dismissible' ? sheetHeight : collapseOffset;
          let next = state.dragStart + pageY - state.startY;
          // Sınırların ötesinde lastik gibi direnç
          if (next < 0) next *= 0.2;
          else if (next > max && mode === 'collapsible') next = max + (next - max) * 0.2;
          translateY.setValue(next);
        },
        onResponderRelease: (event: GestureResponderEvent) =>
          settle(event.nativeEvent.pageY - gesture.current.startY, gesture.current.velocity),
        onResponderTerminate: () => settle(gesture.current.lastY - gesture.current.startY, 0),
        onResponderTerminationRequest: () => false,
      }
    : null;

  const onSheetLayout = (event: LayoutChangeEvent) => setSheetHeight(Math.round(event.nativeEvent.layout.height));
  const onHeaderLayout = (event: LayoutChangeEvent) => {
    const { y, height } = event.nativeEvent.layout;
    setHeaderBottom(Math.round(y + height));
  };

  const ride = variant === 'ride';
  const toggleLabel = mode === 'dismissible' ? 'Paneli kapat' : collapsed ? 'Paneli genişlet' : 'Paneli küçült';

  return (
    <View style={styles.container} pointerEvents="box-none">
      <Animated.View style={{ transform: [{ translateY }], opacity }} pointerEvents="box-none">
        {accessory ? (
          <View style={styles.accessory} pointerEvents="box-none">
            {accessory}
          </View>
        ) : null}
        <View
          style={[styles.sheet, ride && { backgroundColor: theme.ride.bg }, noTextSelection]}
          onLayout={onSheetLayout}
          accessibilityLabel={accessibilityLabel}
          {...dragHandlers}>
          {draggable ? (
            <Pressable
              accessibilityRole="button"
              accessibilityLabel={toggleLabel}
              onPress={() => (mode === 'dismissible' ? dismiss() : setCollapsed((value) => !value))}
              style={styles.grabberArea}>
              <View style={[styles.grabber, ride && { backgroundColor: theme.ride.line }]} />
            </Pressable>
          ) : (
            <View style={styles.grabberSpacer} />
          )}
          <View onLayout={onHeaderLayout} style={styles.header}>
            {notices}
            {header}
          </View>
          {children ? (
            <View
              style={styles.body}
              accessibilityElementsHidden={collapsed}
              importantForAccessibility={collapsed ? 'no-hide-descendants' : 'auto'}>
              {children}
            </View>
          ) : null}
          {collapsed ? (
            // Küçük paneldeki her dokunuş paneli açar; ekran okuyucu için aynı iş tutamaktaki düğmede.
            <Pressable
              accessibilityElementsHidden
              importantForAccessibility="no-hide-descendants"
              tabIndex={-1}
              onPress={() => setCollapsed(false)}
              style={StyleSheet.absoluteFill}
            />
          ) : null}
        </View>
      </Animated.View>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  container: {
    position: 'absolute',
    left: 0,
    right: 0,
    bottom: 0,
  },
  accessory: {
    position: 'absolute',
    right: space.lg,
    bottom: '100%',
    marginBottom: space.md,
    alignItems: 'flex-end',
    gap: space.sm + 2,
  },
  sheet: {
    backgroundColor: t.surface,
    borderTopLeftRadius: radius.sheet,
    borderTopRightRadius: radius.sheet,
    paddingHorizontal: screenPadding,
    paddingBottom: space.lg,
    boxShadow: t.sheetShadow,
  },
  grabberArea: {
    alignItems: 'center',
    paddingTop: space.sm + 2,
    paddingBottom: space.md,
  },
  grabberSpacer: {
    height: space.xl,
  },
  grabber: {
    width: 40,
    height: 5,
    borderRadius: 3,
    backgroundColor: t.lineStrong,
  },
  header: {
    gap: space.md,
  },
  body: {
    gap: space.md,
    paddingTop: space.lg,
  },
}));
