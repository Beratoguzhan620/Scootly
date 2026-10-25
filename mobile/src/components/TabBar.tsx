import type { BottomTabBarProps } from 'expo-router/js-tabs';
import { Pressable, Text, View } from 'react-native';

import { haptics } from '@/lib/haptics';
import { makeStyles, radius, space, textVariants, useTheme } from '@/theme';

/** Alt sekme çubuğu: etkin sekmenin simgesi sarı bir şerit üstünde. */
export function TabBar({ state, descriptors, navigation, insets }: BottomTabBarProps) {
  const theme = useTheme();
  const styles = useStyles();

  return (
    <View style={[styles.bar, { paddingBottom: Math.max(insets.bottom, space.sm) }]} accessibilityRole="tablist">
      {state.routes.map((route, index) => {
        const { options } = descriptors[route.key];
        const focused = state.index === index;
        const label = options.title ?? route.name;

        const onPress = () => {
          const event = navigation.emit({ type: 'tabPress', target: route.key, canPreventDefault: true });
          if (!focused && !event.defaultPrevented) {
            haptics.select();
            navigation.navigate(route.name, route.params);
          }
        };

        return (
          <Pressable
            key={route.key}
            accessibilityRole="tab"
            accessibilityState={{ selected: focused }}
            accessibilityLabel={options.tabBarAccessibilityLabel ?? label}
            testID={options.tabBarButtonTestID}
            onPress={onPress}
            onLongPress={() => navigation.emit({ type: 'tabLongPress', target: route.key })}
            style={styles.item}>
            <View style={[styles.pill, focused && styles.pillActive]}>
              {options.tabBarIcon?.({
                focused,
                color: focused ? theme.onBrand : theme.textMuted,
                size: 24,
              })}
            </View>
            <Text style={[styles.label, focused && styles.labelActive]} numberOfLines={1}>
              {label}
            </Text>
          </Pressable>
        );
      })}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  bar: {
    flexDirection: 'row',
    backgroundColor: t.surface,
    borderTopWidth: 1,
    borderTopColor: t.line,
    paddingTop: space.sm,
  },
  item: {
    flex: 1,
    alignItems: 'center',
    gap: space.xs,
    paddingVertical: space.xxs,
  },
  pill: {
    width: 60,
    height: 32,
    borderRadius: radius.pill,
    alignItems: 'center',
    justifyContent: 'center',
  },
  pillActive: {
    backgroundColor: t.brand,
  },
  label: {
    ...textVariants.captionStrong,
    color: t.textMuted,
  },
  labelActive: {
    color: t.text,
  },
}));
