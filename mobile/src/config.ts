import Constants from 'expo-constants';

/** API'nin yerelde dinlediği port (`launchSettings.json` ve Docker Compose: 5016). */
const DEFAULT_API_PORT = 5016;

/**
 * API adresi şu sırayla belirlenir:
 * 1. `EXPO_PUBLIC_API_URL` (.env.local) — her zaman önceliklidir.
 * 2. Geliştirmede Metro'nun çalıştığı bilgisayar (Expo Go'nun bağlandığı adres) + 5016.
 *    Telefon Metro'ya ulaşabiliyorsa aynı bilgisayardaki API'ye de ulaşabilir.
 * 3. `http://localhost:5016`
 */
export function resolveApiBaseUrl(envValue: string | undefined, hostUri: string | undefined): string {
  const fromEnv = envValue?.trim();
  if (fromEnv) return fromEnv.replace(/\/+$/, '');

  const host = hostUri?.split(':')[0];
  if (host) return `http://${host}:${DEFAULT_API_PORT}`;

  return `http://localhost:${DEFAULT_API_PORT}`;
}

export const appConfig = {
  apiBaseUrl: resolveApiBaseUrl(process.env.EXPO_PUBLIC_API_URL, Constants.expoConfig?.hostUri),
  version: Constants.expoConfig?.version ?? '0.0.0',
};
