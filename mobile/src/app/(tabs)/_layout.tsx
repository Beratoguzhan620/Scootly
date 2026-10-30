import Ionicons from '@expo/vector-icons/Ionicons';
import { Tabs } from 'expo-router/js-tabs';

import { TabBar } from '@/components/TabBar';
import { useTheme } from '@/theme';

/** Harita tam ekran; diğer sekmeler başlığı içerikte büyük yazıyla gösterir. */
export default function TabsLayout() {
  const theme = useTheme();
  return (
    <Tabs
      tabBar={(props) => <TabBar {...props} />}
      screenOptions={{ headerShown: false, sceneStyle: { backgroundColor: theme.bg } }}>
      <Tabs.Screen
        name="index"
        options={{
          title: 'Harita',
          tabBarIcon: ({ color, focused }) => (
            <Ionicons name={focused ? 'map' : 'map-outline'} color={color} size={22} />
          ),
        }}
      />
      <Tabs.Screen
        name="reservations"
        options={{
          title: 'Rezervasyonlar',
          tabBarLabel: 'Rezervasyon',
          tabBarIcon: ({ color, focused }) => (
            <Ionicons name={focused ? 'timer' : 'timer-outline'} color={color} size={22} />
          ),
        }}
      />
      <Tabs.Screen
        name="rides"
        options={{
          title: 'Sürüşlerim',
          tabBarIcon: ({ color, focused }) => (
            <Ionicons name={focused ? 'receipt' : 'receipt-outline'} color={color} size={22} />
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
