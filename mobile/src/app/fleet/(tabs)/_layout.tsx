import Ionicons from '@expo/vector-icons/Ionicons';
import { Tabs } from 'expo-router/js-tabs';

import { TabBar } from '@/components/TabBar';
import { useTheme } from '@/theme';

/** Filo ekibinin sekmeleri: harita, araç listesi, hesap. */
export default function FleetTabsLayout() {
  const theme = useTheme();
  return (
    <Tabs
      tabBar={(props) => <TabBar {...props} />}
      screenOptions={{ headerShown: false, sceneStyle: { backgroundColor: theme.bg } }}>
      <Tabs.Screen
        name="index"
        options={{
          title: 'Filo',
          tabBarIcon: ({ color, focused }) => (
            <Ionicons name={focused ? 'map' : 'map-outline'} color={color} size={22} />
          ),
        }}
      />
      <Tabs.Screen
        name="vehicles"
        options={{
          title: 'Araçlar',
          tabBarIcon: ({ color, focused }) => (
            <Ionicons name={focused ? 'list' : 'list-outline'} color={color} size={22} />
          ),
        }}
      />
      <Tabs.Screen
        name="account"
        options={{
          title: 'Hesap',
          tabBarIcon: ({ color, focused }) => (
            <Ionicons name={focused ? 'person' : 'person-outline'} color={color} size={22} />
          ),
        }}
      />
    </Tabs>
  );
}
