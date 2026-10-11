import { useState, type Ref } from 'react';
import { StyleSheet, Text, TextInput, View, type StyleProp, type TextInputProps, type ViewStyle } from 'react-native';

import { colors, radius, shadows, spacing, typography } from '@/theme';

type Props = TextInputProps & {
  label: string;
  /** `.invalid-feedback` */
  error?: string | null;
  /** `.form-text` */
  hint?: string;
  containerStyle?: StyleProp<ViewStyle>;
  ref?: Ref<TextInput>;
};

/** Bootstrap `.form-label` + `.form-control`; odak halkası paneldeki gibi #258cfb. */
export function TextField({ label, error, hint, containerStyle, onFocus, onBlur, style, ref, ...rest }: Props) {
  const [focused, setFocused] = useState(false);
  const invalid = Boolean(error);

  return (
    <View style={[styles.container, containerStyle]}>
      <Text style={styles.label}>{label}</Text>
      <TextInput
        ref={ref}
        accessibilityLabel={label}
        accessibilityHint={invalid ? (error ?? undefined) : hint}
        placeholderTextColor={colors.muted}
        style={[
          styles.input,
          focused && styles.focused,
          invalid && styles.invalid,
          focused && invalid && styles.invalidFocused,
          style,
        ]}
        onFocus={(event) => {
          setFocused(true);
          onFocus?.(event);
        }}
        onBlur={(event) => {
          setFocused(false);
          onBlur?.(event);
        }}
        {...rest}
      />
      {invalid ? <Text style={styles.error}>{error}</Text> : hint ? <Text style={styles.hint}>{hint}</Text> : null}
    </View>
  );
}

const styles = StyleSheet.create({
  container: {
    marginBottom: spacing[3],
  },
  label: {
    ...typography.body,
    color: colors.body,
    marginBottom: spacing[2],
  },
  input: {
    ...typography.body,
    color: colors.body,
    backgroundColor: colors.bodyBg,
    minHeight: 44,
    paddingHorizontal: 12,
    paddingVertical: 8,
    borderWidth: 1,
    borderColor: colors.border,
    borderRadius: radius.md,
    // Web önizlemesinde tarayıcının kendi odak çerçevesi yerine panelin halkası görünsün.
    outlineWidth: 0,
  },
  focused: {
    borderColor: colors.focusBorder,
    boxShadow: shadows.focus,
  },
  invalid: {
    borderColor: colors.danger,
  },
  invalidFocused: {
    boxShadow: '0px 0px 0px 4px rgba(220, 53, 69, 0.25)',
  },
  error: {
    ...typography.small,
    color: colors.danger,
    marginTop: spacing[1],
  },
  hint: {
    ...typography.small,
    color: colors.muted,
    marginTop: spacing[1],
  },
});
