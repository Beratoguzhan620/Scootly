import { Link, useRouter } from 'expo-router';
import { useRef, useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, StyleSheet, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { errorMessage } from '@/api/client';
import { useApiBaseUrl } from '@/api/server';
import { useAuth } from '@/auth/AuthProvider';
import { AlertBox } from '@/components/AlertBox';
import { AppText, Code } from '@/components/AppText';
import { Button } from '@/components/Button';
import { PanelFooter } from '@/components/PanelFooter';
import { TextField } from '@/components/TextField';
import { appConfig } from '@/config';
import { validateEmail } from '@/domain/validation';
import { isLoopbackUrl } from '@/lib/url';
import { colors, spacing, typography } from '@/theme';

export default function SignInScreen() {
  const insets = useSafeAreaInsets();
  const router = useRouter();
  const apiBaseUrl = useApiBaseUrl();
  const { signIn, notice, clearNotice } = useAuth();
  const passwordRef = useRef<TextInput>(null);

  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [fieldErrors, setFieldErrors] = useState<{ email?: string | null; password?: string | null }>({});
  const [error, setError] = useState<string | null>(null);
  const [submitting, setSubmitting] = useState(false);

  const submit = async () => {
    const errors = {
      email: validateEmail(email),
      password: password ? null : 'Parola zorunludur.',
    };
    setFieldErrors(errors);
    if (errors.email || errors.password) return;

    setError(null);
    setSubmitting(true);
    try {
      await signIn(email, password);
      // Başarılı girişte korumalı yönlendirme haritaya geçer; bu ekran kapanır.
    } catch (caught) {
      setError(errorMessage(caught));
      setSubmitting(false);
    }
  };

  return (
    <View style={styles.root}>
      {/* Panelin koyu navbar'ı: solda marka. */}
      <View style={[styles.brandBar, { paddingTop: insets.top + spacing[2] }]}>
        <Text style={styles.brand} accessibilityRole="header">
          Scootly
        </Text>
        <Text style={styles.tagline}>Şehir içi elektrikli scooter paylaşımı</Text>
      </View>

      <KeyboardAvoidingView style={styles.root} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView
          contentContainerStyle={[styles.content, { paddingBottom: insets.bottom + spacing[3] }]}
          keyboardShouldPersistTaps="handled">
          <AppText variant="h1">Giriş Yap</AppText>

          {notice ? (
            <AlertBox tone={notice.tone} onDismiss={clearNotice}>
              {notice.message}
            </AlertBox>
          ) : null}
          {error ? <AlertBox tone="danger">{error}</AlertBox> : null}
          {appConfig.serverEditable && isLoopbackUrl(apiBaseUrl) ? (
            <AlertBox tone="info" action={{ label: 'Sunucu adresini değiştir', onPress: () => router.push('/server') }}>
              Sunucu adresi bu cihazın kendisini gösteriyor (localhost). Telefonda deniyorsan API&apos;nin çalıştığı
              bilgisayarın IP adresini gir.
            </AlertBox>
          ) : null}

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
              autoComplete="current-password"
              textContentType="password"
              returnKeyType="go"
              onSubmitEditing={() => void submit()}
              editable={!submitting}
            />
            <Button title="Giriş Yap" onPress={() => void submit()} loading={submitting} />
          </View>

          <AppText>
            Hesabın yok mu?{' '}
            <Link href="/sign-up" style={styles.link}>
              Kayıt ol
            </Link>
          </AppText>

          <PanelFooter>
            {appConfig.serverEditable ? (
              <View style={styles.server}>
                <AppText variant="small" muted style={styles.serverText}>
                  Sunucu: <Code>{apiBaseUrl}</Code>
                </AppText>
                <Link href="/server" style={styles.serverLink} accessibilityLabel="Sunucu ayarları">
                  Değiştir
                </Link>
              </View>
            ) : null}
          </PanelFooter>
        </ScrollView>
      </KeyboardAvoidingView>
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    flex: 1,
    backgroundColor: colors.bodyBg,
  },
  brandBar: {
    backgroundColor: colors.navbar,
    paddingHorizontal: spacing[3],
    paddingBottom: spacing[3],
    gap: 2,
  },
  brand: {
    ...typography.brand,
    color: colors.navbarText,
  },
  tagline: {
    ...typography.small,
    color: colors.navbarSubtle,
  },
  content: {
    flexGrow: 1,
    padding: spacing[3],
    gap: spacing[3],
  },
  link: {
    color: colors.link,
    textDecorationLine: 'underline',
  },
  server: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing[2],
  },
  serverText: {
    flexShrink: 1,
  },
  serverLink: {
    ...typography.small,
    color: colors.link,
    textDecorationLine: 'underline',
    paddingVertical: spacing[2],
  },
});
