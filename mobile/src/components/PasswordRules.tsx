import Ionicons from '@expo/vector-icons/Ionicons';
import { StyleSheet, Text, View } from 'react-native';

import { checkPassword } from '@/domain/validation';
import { colors, spacing, typography } from '@/theme';

/** Parola yazılırken Identity kurallarını canlı gösteren liste. */
export function PasswordRules({ password }: { password: string }) {
  return (
    <View style={styles.list} accessibilityLabel="Parola kuralları">
      {checkPassword(password).map((rule) => (
        <View key={rule.key} style={styles.row} accessibilityState={{ checked: rule.met }}>
          <Ionicons
            name={rule.met ? 'checkmark-circle' : 'ellipse-outline'}
            size={16}
            color={rule.met ? colors.success : colors.muted}
          />
          <Text style={[styles.text, rule.met && styles.met]}>{rule.label}</Text>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: spacing[1],
    marginTop: -spacing[2],
    marginBottom: spacing[3],
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: 6,
  },
  text: {
    ...typography.small,
    color: colors.muted,
  },
  met: {
    color: colors.success,
  },
});
