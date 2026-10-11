import Ionicons from '@expo/vector-icons/Ionicons';
import { Tabs } from 'expo-router/js-tabs';
import { StyleSheet, Text } from 'react-native';

import { useAuth } from '@/auth/AuthProvider';
import { colors, typography } from '@/theme';
import { headerOptions } from '@/theme/navigation';

/** Paneldeki gibi sağda oturum açan kullanıcının e-postası (`text-white-50`). */
function SignedInUser() {
  const { state } = useAuth();
  if (state.status !== 'signedIn') return null;
  return (
    <Text numberOfLines={1} style={styles.user}>
      {state.account.email}
    </Text>
  );
}

/** Alt sekme çubuğu panelin koyu menüsüyle aynı: etkin sekme beyaz, diğerleri %55 beyaz. */
export default function TabsLayout() {
  return (
    <Tabs
      screenOptions={{
        ...headerOptions,
        tabBarStyle: { backgroundColor: colors.navbar, borderTopColor: colors.navbar },
        tabBarActiveTintColor: colors.navbarText,
        tabBarInactiveTintColor: colors.navbarLink,
        sceneStyle: { backgroundColor: colors.bodyBg },
      }}>
      <Tabs.Screen
        name="index"
        options={{
          title: 'Harita',
          headerTitle: 'Scootly',
          headerRight: () => <SignedInUser />,
          tabBarIcon: ({ color, size }) => <Ionicons name="map-outline" color={color} size={size} />,
        }}
      />
      <Tabs.Screen
        name="rides"
        options={{
          title: 'Sürüşlerim',
          tabBarIcon: ({ color, size }) => <Ionicons name="receipt-outline" color={color} size={size} />,
        }}
      />
      <Tabs.Screen
        name="account"
        options={{
          title: 'Hesap',
          tabBarIcon: ({ color, size }) => <Ionicons name="person-circle-outline" color={color} size={size} />,
        }}
      />
    </Tabs>
  );
}

const styles = StyleSheet.create({
  user: {
    ...typography.small,
    color: colors.navbarSubtle,
    maxWidth: 200,
    marginRight: 16,
  },
});
