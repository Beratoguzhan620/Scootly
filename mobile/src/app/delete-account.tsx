import { useState } from 'react';
import { errorMessage } from '@/api/client';
import { accountApi } from '@/api/scootly';
import { notices, useAuth } from '@/auth/AuthProvider';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { confirm } from '@/lib/confirm';

export default function DeleteAccountScreen() {
  const { signOut } = useAuth();
  const [password, setPassword] = useState('');
  const [fieldError, setFieldError] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const submit = async () => {
    if (!password) {
      setFieldError('Hesabı silmek için parolanı gir.');
      return;
    }
    setFieldError(null);

    const confirmed = await confirm({
      title: 'Hesabı kalıcı olarak sil',
      message: 'Bu işlem geri alınamaz. Devam etmek istiyor musun?',
      confirmLabel: 'Sil',
      destructive: true,
    });
    if (!confirmed) return;

    setError(null);
    setSubmitting(true);
    try {
      await accountApi.remove(password);
      // Oturum kapanınca korumalı yönlendirme giriş ekranına döner.
      await signOut(notices.accountDeleted);
    } catch (caught) {
      setError(errorMessage(caught));
      setSubmitting(false);
    }
  };

  return (
    <Screen keyboard edges={['bottom']}>
      <Notice tone="danger" title="Bu işlem geri alınamaz">
        Hesabın kalıcı olarak silinir ve sürüş konumların anonimleştirilir. Açık bir rezervasyonun, devam eden sürüşün
        ya da ödenmemiş bir ücretin varsa hesap silinemez.
      </Notice>

      {error ? <Notice tone="warning">{error}</Notice> : null}

      <TextField
        label="Parola"
        hint="Güvenliğin için hesabı parolanla onaylıyoruz."
        value={password}
        onChangeText={setPassword}
        error={fieldError}
        secureTextEntry
        autoCapitalize="none"
        autoComplete="current-password"
        textContentType="password"
        returnKeyType="done"
        editable={!submitting}
      />
      <Button
        title="Hesabımı kalıcı olarak sil"
        variant="danger"
        size="lg"
        onPress={() => void submit()}
        loading={submitting}
      />
    </Screen>
  );
}
