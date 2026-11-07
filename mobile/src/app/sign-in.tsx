import { Link, useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useRef, useState } from 'react';
import { KeyboardAvoidingView, Platform, ScrollView, Text, TextInput, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { errorMessage } from '@/api/client';
import { useApiBaseUrl } from '@/api/server';
import { useAuth } from '@/auth/AuthProvider';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { TextField } from '@/components/TextField';
import { Wordmark } from '@/components/Wordmark';
import { appConfig } from '@/config';
import { RESERVATION_DURATION_MS, STANDARD_TARIFF } from '@/domain/rules';
import { validateEmail } from '@/domain/validation';
import { formatCurrency } from '@/lib/format';
import { isLoopbackUrl } from '@/lib/url';
import { INK, makeStyles, radius, screenPadding, space, textVariants } from '@/theme';

/** Fiyatlar tabela levhası gibi: kayıt olmadan önce ne ödeneceği görünsün. */
const tariffPlates = [
  STANDARD_TARIFF.unlockFee === 0 ? 'Açılış ücretsiz' : `Açılış ${formatCurrency(STANDARD_TARIFF.unlockFee)}`,
  `Dakikası ${formatCurrency(STANDARD_TARIFF.perMinuteRate)}`,
  `${RESERVATION_DURATION_MS / 60_000} dk rezervasyon`,
];

export default function SignInScreen() {
  const insets = useSafeAreaInsets();
  const styles = useStyles();
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
      password: password ? null : 'Parolanı gir.',
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
      {/* Üstteki sarı levha her iki temada da aynı; üstündeki durum çubuğu koyu yazılı. */}
      <StatusBar style="dark" />
      <KeyboardAvoidingView style={styles.root} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
        <ScrollView
          contentContainerStyle={[styles.scroll, { paddingBottom: insets.bottom + space.xxl }]}
          keyboardShouldPersistTaps="handled">
          <View style={[styles.hero, { paddingTop: insets.top + space.xxxl }]}>
            <Wordmark size={72} />
            <Text style={styles.tagline}>Şehir içi elektrikli scooter paylaşımı</Text>
            <View style={styles.plates}>
              {tariffPlates.map((label) => (
                <View key={label} style={styles.plate}>
                  <Text style={styles.plateText}>{label}</Text>
                </View>
              ))}
            </View>
          </View>

          <View style={styles.form}>
            <AppText variant="title">Giriş yap</AppText>

            {notice ? (
              <Notice tone={notice.tone} onDismiss={clearNotice}>
                {notice.message}
              </Notice>
            ) : null}
            {error ? <Notice tone="danger">{error}</Notice> : null}
            {appConfig.serverEditable && isLoopbackUrl(apiBaseUrl) ? (
              <Notice tone="info" action={{ label: 'Sunucu adresini değiştir', onPress: () => router.push('/server') }}>
                Sunucu adresi bu cihazın kendisini gösteriyor (localhost). Telefonda deniyorsan API&apos;nin çalıştığı
                bilgisayarın adresini gir.
              </Notice>
            ) : null}

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
            <Button title="Giriş yap" variant="strong" size="lg" onPress={() => void submit()} loading={submitting} />

            <View style={styles.signUpRow}>
              <AppText muted>Hesabın yok mu?</AppText>
              <Link href="/sign-up" style={styles.link}>
                Kayıt ol
              </Link>
            </View>
          </View>

          {appConfig.serverEditable ? (
            <View style={styles.server}>
              <View style={styles.serverText}>
                <AppText variant="caption" muted>
                  Sunucu
                </AppText>
                <AppText variant="caption" numberOfLines={1}>
                  {apiBaseUrl}
                </AppText>
              </View>
              <Link href="/server" style={styles.link} accessibilityLabel="Sunucu ayarları">
                Değiştir
              </Link>
            </View>
          ) : null}
        </ScrollView>
      </KeyboardAvoidingView>
    </View>
  );
}

const useStyles = makeStyles((t) => ({
  root: {
    flex: 1,
    backgroundColor: t.bg,
  },
  scroll: {
    flexGrow: 1,
  },
  hero: {
    backgroundColor: t.brand,
    paddingHorizontal: screenPadding,
    paddingBottom: space.xxl + 4,
    borderBottomLeftRadius: radius.sheet,
    borderBottomRightRadius: radius.sheet,
    gap: space.sm,
  },
  tagline: {
    ...textVariants.bodyStrong,
    fontSize: 17,
    color: INK,
    marginTop: -space.xs,
  },
  plates: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: space.sm,
    marginTop: space.md,
  },
  plate: {
    borderWidth: 1.5,
    borderColor: INK,
    borderRadius: radius.plate,
    paddingHorizontal: space.sm + 2,
    paddingVertical: 3,
  },
  plateText: {
    ...textVariants.captionStrong,
    fontSize: 14,
    color: INK,
  },
  form: {
    paddingHorizontal: screenPadding,
    paddingTop: space.xxl + 4,
    gap: space.lg,
  },
  signUpRow: {
    flexDirection: 'row',
    justifyContent: 'center',
    alignItems: 'center',
    gap: space.xs + 2,
    paddingVertical: space.xs,
  },
  link: {
    ...textVariants.bodyStrong,
    color: t.text,
    textDecorationLine: 'underline',
    paddingVertical: space.sm,
  },
  server: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.md,
    marginTop: 'auto',
    marginHorizontal: screenPadding,
    paddingTop: space.xxl,
  },
  serverText: {
    flex: 1,
    gap: 1,
  },
}));
