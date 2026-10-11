import { useRouter } from 'expo-router';
import { useState } from 'react';
import { StyleSheet, View } from 'react-native';

import { checkServer, serverStore, useApiBaseUrl, type ServerCheck } from '@/api/server';
import { AlertBox } from '@/components/AlertBox';
import { AppText, Code } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { normalizeApiUrl } from '@/lib/url';
import { spacing } from '@/theme';

/** Test sürümleri için: uygulamanın bağlanacağı API adresini değiştirme ve bağlantıyı deneme. */
export default function ServerSettingsScreen() {
  const router = useRouter();
  const current = useApiBaseUrl();
  const [value, setValue] = useState(current);
  const [error, setError] = useState<string | null>(null);
  const [result, setResult] = useState<ServerCheck | null>(null);
  const [testing, setTesting] = useState(false);
  const [saving, setSaving] = useState(false);

  const parse = (): string | null => {
    const url = normalizeApiUrl(value);
    setError(url ? null : 'Geçerli bir adres girin. Örnek: http://192.168.1.20:5017');
    return url;
  };

  const test = async () => {
    const url = parse();
    if (!url) return;
    setValue(url);
    setResult(null);
    setTesting(true);
    setResult(await checkServer(url));
    setTesting(false);
  };

  const save = async () => {
    const url = parse();
    if (!url) return;
    setSaving(true);
    await serverStore.set(url);
    router.back();
  };

  const reset = async () => {
    await serverStore.set(null);
    setValue(serverStore.defaultUrl());
    setResult(null);
    setError(null);
  };

  return (
    <Screen keyboard edges={['bottom']}>
      <AlertBox tone="info">
        Telefon ve API&apos;nin çalıştığı bilgisayar aynı Wi-Fi ağında olmalı. Adres, bilgisayarın yerel IP&apos;si ve
        API&apos;nin portudur.
      </AlertBox>

      <View>
        <TextField
          label="API adresi"
          value={value}
          onChangeText={(text) => {
            setValue(text);
            setResult(null);
          }}
          error={error}
          hint="Örnek: http://192.168.1.20:5017 — bilgisayarın IP'sini Wi-Fi ayarlarından öğrenebilirsin."
          placeholder="http://192.168.1.20:5017"
          keyboardType="url"
          autoCapitalize="none"
          autoCorrect={false}
          autoComplete="off"
          textContentType="URL"
          returnKeyType="done"
          onSubmitEditing={() => void test()}
          editable={!saving}
        />

        {result ? <AlertBox tone={result.tone}>{result.message}</AlertBox> : null}

        <View style={[styles.actions, result ? styles.actionsAfterResult : null]}>
          <Button
            title="Bağlantıyı Test Et"
            variant="outline-primary"
            loading={testing}
            disabled={saving}
            onPress={() => void test()}
          />
          <Button title="Kaydet" loading={saving} disabled={testing} onPress={() => void save()} />
        </View>
      </View>

      <View style={styles.info}>
        <AppText variant="small" muted>
          Şu an kullanılan: <Code>{current}</Code>
        </AppText>
        {serverStore.isCustom() ? (
          <AppText variant="small" muted>
            Varsayılan: <Code>{serverStore.defaultUrl()}</Code>
          </AppText>
        ) : null}
      </View>
      {serverStore.isCustom() ? <Button title="Varsayılana Dön" variant="link" onPress={() => void reset()} /> : null}
    </Screen>
  );
}

const styles = StyleSheet.create({
  actions: {
    gap: spacing[2],
  },
  actionsAfterResult: {
    marginTop: spacing[3],
  },
  info: {
    gap: spacing[1],
  },
});
