import { useRef, useState } from 'react';
import { TextInput, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { useAuth } from '@/auth/AuthProvider';
import { AlertBox } from '@/components/AlertBox';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { PasswordRules } from '@/components/PasswordRules';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { validateEmail, validatePassword } from '@/domain/validation';

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
      <AppText variant="lead">Hesabını oluştur, yakındaki scooter&apos;ları hemen kiralamaya başla.</AppText>

      {error ? <AlertBox tone="danger">{error}</AlertBox> : null}

      <View>
        <TextField
          label="E-posta"
          value={email}
          onChangeText={setEmail}
          error={fieldErrors.email}
          keyboardType="email-address"
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete="email"
          textContentType="username"
          returnKeyType="next"
          onSubmitEditing={() => passwordRef.current?.focus()}
          editable={!submitting}
        />
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
        <Button title="Kayıt Ol" onPress={() => void submit()} loading={submitting} />
      </View>

      <AppText variant="small" muted>
        Kayıt olan hesaplar sürücü olarak açılır. Filo ve saha ekibi hesapları web panelinden yönetilir.
      </AppText>
    </Screen>
  );
}
