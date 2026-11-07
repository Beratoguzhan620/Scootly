import { useRouter } from 'expo-router';
import { useRef, useState } from 'react';
import { TextInput, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { accountApi } from '@/api/scootly';
import { useAuth } from '@/auth/AuthProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { PasswordRules } from '@/components/PasswordRules';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { validatePassword } from '@/domain/validation';
import { space } from '@/theme';

type FieldErrors = { current?: string | null; next?: string | null; confirmation?: string | null };

export default function ChangePasswordScreen() {
  const router = useRouter();
  const { replaceToken } = useAuth();
  const nextRef = useRef<TextInput>(null);
  const confirmationRef = useRef<TextInput>(null);

  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirmation, setConfirmation] = useState('');
  const [fieldErrors, setFieldErrors] = useState<FieldErrors>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);
  const [done, setDone] = useState(false);

  const submit = async () => {
    const errors: FieldErrors = {
      current: current ? null : 'Mevcut parolanı gir.',
      next: validatePassword(next) ?? (next === current ? 'Yeni parola mevcut paroladan farklı olmalı.' : null),
      confirmation: confirmation === next ? null : 'Parolalar eşleşmiyor.',
    };
    setFieldErrors(errors);
    if (errors.current || errors.next || errors.confirmation) return;

    setError(null);
    setSubmitting(true);
    try {
      // Sunucu eski token'ları geçersiz kılar ve yenisini döner.
      const { token } = await accountApi.changePassword(current, next);
      await replaceToken(token);
      setDone(true);
    } catch (caught) {
      setError(errorMessage(caught));
    } finally {
      setSubmitting(false);
    }
  };

  if (done) {
    return (
      <Screen edges={['bottom']}>
        <Notice tone="success" title="Parolan değiştirildi">
          Diğer cihazlardaki oturumların kapatıldı; bu cihazda oturumun açık kalıyor.
        </Notice>
        <Button title="Tamam" size="lg" onPress={() => router.back()} />
      </Screen>
    );
  }

  return (
    <Screen keyboard edges={['bottom']}>
      <AppText muted>Parolan değişince diğer cihazlardaki oturumların kapanır.</AppText>

      {error ? <Notice tone="danger">{error}</Notice> : null}

      <TextField
        label="Mevcut parola"
        value={current}
        onChangeText={setCurrent}
        error={fieldErrors.current}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="current-password"
        textContentType="password"
        returnKeyType="next"
        onSubmitEditing={() => nextRef.current?.focus()}
        editable={!submitting}
      />
      <View style={{ gap: space.md }}>
        <TextField
          ref={nextRef}
          label="Yeni parola"
          value={next}
          onChangeText={setNext}
          error={fieldErrors.next}
          secureTextEntry
          autoCapitalize="none"
          autoComplete="new-password"
          textContentType="newPassword"
          returnKeyType="next"
          onSubmitEditing={() => confirmationRef.current?.focus()}
          editable={!submitting}
        />
        <PasswordRules password={next} />
      </View>
      <TextField
        ref={confirmationRef}
        label="Yeni parola (tekrar)"
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
      <Button title="Parolayı değiştir" size="lg" onPress={() => void submit()} loading={submitting} />
    </Screen>
  );
}
