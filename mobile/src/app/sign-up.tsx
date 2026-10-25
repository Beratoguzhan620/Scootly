import { useRef, useState } from 'react';
import { TextInput, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { PasswordRules } from '@/components/PasswordRules';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { validateEmail, validatePassword } from '@/domain/validation';
import { space } from '@/theme';

type FieldErrors = { email?: string | null; password?: string | null; confirmation?: string | null };

export default function SignUpScreen() {
  const { signUp } = useAuth();
  const passwordRef = useRef<TextInput>(null);
  const confirmationRef = useRef<TextInput>(null);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const submit = async () => {
    const errors: FieldErrors = {
      email: validateEmail(email),
      password: validatePassword(password),
      confirmation: confirmation === password ? null : 'Parolalar eşleşmiyor.',
    };
    setFieldErrors(errors);
    if (errors.email || errors.password || errors.confirmation) return;

    setError(null);
    setSubmitting(true);
    try {
      await signUp(email, password);
    } catch (caught) {
      setError(errorMessage(caught));
      setSubmitting(false);
    }
  };

  return (
    <Screen keyboard edges={['bottom']}>
      <AppText muted>Hesabını oluştur, yakındaki scooter&apos;ları hemen kiralamaya başla.</AppText>

      {error ? <Notice tone="danger">{error}</Notice> : null}

      <TextField
        label="E-posta"
        value={email}
        onChangeText={setEmail}
        error={fieldErrors.email}
        placeholder="ornek@eposta.com"
        keyboardType="email-address"
        autoCapitalize="none"
        autoCorrect={false}
        autoComplete="email"
        textContentType="username"
        returnKeyType="next"
        onSubmitEditing={() => passwordRef.current?.focus()}
        editable={!submitting}
      />
      <View style={{ gap: space.md }}>
        <TextField
          ref={passwordRef}
          label="Parola"
          value={password}
          onChangeText={setPassword}
          error={fieldErrors.password}
          secureTextEntry
          autoCapitalize="none"
          autoComplete="new-password"
          textContentType="newPassword"
          returnKeyType="next"
          onSubmitEditing={() => confirmationRef.current?.focus()}
          editable={!submitting}
        />
        <PasswordRules password={password} />
      </View>
      <TextField
        ref={confirmationRef}
        label="Parola (tekrar)"
        value={confirmation}
        onChangeText={setConfirmation}
        error={fieldErrors.confirmation}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="new-password"
        textContentType="newPassword"
        returnKeyType="go"
        onSubmitEditing={() => void submit()}
        editable={!submitting}
      />
      <Button title="Kayıt ol" size="lg" onPress={() => void submit()} loading={submitting} />

      <AppText variant="caption" muted>
        Kayıt olan hesaplar sürücü olarak açılır. Filo ve saha ekibi hesapları web panelinden yönetilir.
      </AppText>
    </Screen>
  );
}
