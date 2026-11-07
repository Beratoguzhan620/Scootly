import { useState, type Ref } from 'react';
import { Pressable, Text, TextInput, View, type StyleProp, type TextInputProps, type ViewStyle } from 'react-native';

import { Icon } from './Icon';
import { makeStyles, radius, space, textVariants, useTheme } from '@/theme';

type Props = TextInputProps & {
  label: string;
  error?: string | null;
  hint?: string;
  containerStyle?: StyleProp<ViewStyle>;
  ref?: Ref<TextInput>;
};

/** Etiketli form alanı. Parola alanlarında yazılanı gösterip gizleyen bir göz düğmesi vardır. */
export function TextField({
  label,
  error,
  hint,
  containerStyle,
  onFocus,
  onBlur,
  style,
  ref,
  secureTextEntry,
  ...rest
}: Props) {
  const theme = useTheme();
  const styles = useStyles();
  const [focused, setFocused] = useState(false);
  const [revealed, setRevealed] = useState(false);
  const invalid = Boolean(error);
  const secure = Boolean(secureTextEntry);

  return (
    <View style={[styles.container, containerStyle]}>
      <Text style={styles.label}>{label}</Text>
      <View style={[styles.field, focused && styles.focused, invalid && styles.invalid]}>
        <TextInput
          ref={ref}
          accessibilityLabel={label}
          accessibilityHint={invalid ? (error ?? undefined) : hint}
          placeholderTextColor={theme.textSubtle}
          selectionColor={theme.focus}
          cursorColor={theme.focus}
          secureTextEntry={secure && !revealed}
          style={[styles.input, style]}
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
        {secure ? (
          <Pressable
            accessibilityRole="button"
            accessibilityLabel={revealed ? 'Parolayı gizle' : 'Parolayı göster'}
            hitSlop={8}
            onPress={() => setRevealed((value) => !value)}
            style={styles.reveal}>
            <Icon name={revealed ? 'eye-off-outline' : 'eye-outline'} size={22} color={theme.textMuted} />
          </Pressable>
        ) : null}
      </View>
      {invalid ? (
        <View style={styles.messageRow}>
          <Icon name="alert-circle" size={16} color={theme.critical.fg} />
          <Text style={[styles.message, { color: theme.critical.fg }]}>{error}</Text>
        </View>
      ) : hint ? (
        <Text style={[styles.message, styles.hint]}>{hint}</Text>
      ) : null}
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  container: {
    gap: space.sm,
  },
  label: {
    ...textVariants.label,
    color: t.text,
  },
  field: {
    flexDirection: 'row',
    alignItems: 'center',
    minHeight: 52,
    borderRadius: radius.control,
    borderWidth: 1.5,
    borderColor: t.lineStrong,
    backgroundColor: t.surface,
  },
  focused: {
    borderColor: t.focus,
  },
  invalid: {
    borderColor: t.critical.solid,
  },
  input: {
    ...textVariants.body,
    flex: 1,
    color: t.text,
    paddingHorizontal: space.lg,
    paddingVertical: space.md,
    // Web önizlemesinde tarayıcının kendi odak çerçevesi görünmesin; odak, alanın çerçevesinde.
    outlineWidth: 0,
  },
  reveal: {
    width: 48,
    alignSelf: 'stretch',
    alignItems: 'center',
    justifyContent: 'center',
  },
  messageRow: {
    flexDirection: 'row',
    alignItems: 'flex-start',
    gap: space.xs + 2,
  },
  message: {
    ...textVariants.caption,
    flexShrink: 1,
  },
  hint: {
    color: t.textMuted,
  },
}));
