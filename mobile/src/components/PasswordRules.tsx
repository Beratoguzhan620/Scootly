import { StyleSheet, Text, View } from 'react-native';

import { Icon } from './Icon';
import { checkPassword } from '@/domain/validation';
import { space, textVariants, useTheme } from '@/theme';

/** Parola yazılırken kuralları canlı gösteren liste. */
export function PasswordRules({ password }: { password: string }) {
  const theme = useTheme();
  return (
    <View style={styles.list} accessibilityLabel="Parola kuralları">
      {checkPassword(password).map((rule) => (
        <View key={rule.key} style={styles.row} accessibilityState={{ checked: rule.met }}>
          <Icon
            name={rule.met ? 'checkmark-circle' : 'ellipse-outline'}
            size={17}
            color={rule.met ? theme.positive.solid : theme.textSubtle}
          />
          <Text style={[textVariants.caption, { color: rule.met ? theme.positive.fg : theme.textMuted }]}>
            {rule.label}
          </Text>
        </View>
      ))}
    </View>
  );
}

const styles = StyleSheet.create({
  list: {
    gap: space.xs + 2,
    paddingHorizontal: space.xxs,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
  },
});
