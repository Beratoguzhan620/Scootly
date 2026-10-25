import { useRouter } from 'expo-router';
import { useState } from 'react';
import { View } from 'react-native';

import { checkServer, serverStore, useApiBaseUrl, type ServerCheck } from '@/api/server';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { InfoRow, RowGroup } from '@/components/Rows';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { normalizeApiUrl } from '@/lib/url';
import { space } from '@/theme';

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
    setError(url ? null : 'Geçerli bir adres gir. Örnek: http://192.168.1.20:5017');
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
      <AppText muted>
        API&apos;nin çalıştığı bilgisayarın yerel adresini ve portunu (telefon aynı Wi-Fi ağındaysa) ya da internet
        tünelinin https adresini gir.
      </AppText>

      <TextField
        label="API adresi"
        value={value}
        onChangeText={(text) => {
          setValue(text);
          setResult(null);
        }}
        error={error}
        hint="Bilgisayarın IP adresini Wi-Fi ayarlarından öğrenebilirsin."
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

      {result ? <Notice tone={result.tone}>{result.message}</Notice> : null}

      <View style={{ gap: space.sm + 2 }}>
        <Button
          title="Bağlantıyı test et"
          variant="secondary"
          icon="pulse"
          loading={testing}
          disabled={saving}
          onPress={() => void test()}
        />
        <Button title="Kaydet" loading={saving} disabled={testing} onPress={() => void save()} />
      </View>

      <RowGroup>
        <InfoRow label="Şu an kullanılan" value={current} />
        {serverStore.isCustom() ? <InfoRow label="Varsayılan" value={serverStore.defaultUrl()} /> : null}
      </RowGroup>
      {serverStore.isCustom() ? <Button title="Varsayılana dön" variant="ghost" onPress={() => void reset()} /> : null}
    </Screen>
  );
}
