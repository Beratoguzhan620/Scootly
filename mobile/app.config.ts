import type { ExpoConfig } from 'expo/config';
import { withXcodeProject, type ConfigPlugin } from 'expo/config-plugins';

/** Marka sarısı (voltaj sarısı); simge, açılış ekranı ve uygulama içi açılış görünümü aynı zemini kullanır. */
const BRAND_VOLT = '#FFD60A';

/**
 * Release derlemede API'ye düz HTTP ile bağlanılabilsin mi? Test sürümlerinde API, bilgisayarın yerel ağ
 * adresinde HTTP ile çalışıyor; Android 9+ ise release derlemelerde HTTP'yi varsayılan olarak engeller.
 * Mağaza sürümünde API HTTPS ile yayınlanır ve derleme `SCOOTLY_ALLOW_HTTP=false` ile alınır.
 */
const allowHttp = process.env.SCOOTLY_ALLOW_HTTP !== 'false';

/**
 * Her derleme bir öncekinin üstüne kurulabilsin diye artan derleme numarası. Derleme ortamı
 * `SCOOTLY_VERSION_CODE` ile verebilir; verilmezse sürümle birlikte elle artırılır (0.2.0 → 2).
 */
const versionCode = Number(process.env.SCOOTLY_VERSION_CODE) || 2;

const config: ExpoConfig = {
  name: 'Scootly',
  slug: 'scootly-mobile',
  scheme: 'scootly',
  version: '0.2.0',
  orientation: 'portrait',
  icon: './assets/icon.png',
  // Uygulama telefonun açık/koyu tema ayarını izler.
  userInterfaceStyle: 'automatic',
  ios: {
    supportsTablet: true,
    bundleIdentifier: 'com.scootly.mobile',
    buildNumber: String(versionCode),
  },
  android: {
    package: 'com.scootly.mobile',
    versionCode,
    adaptiveIcon: {
      backgroundColor: BRAND_VOLT,
      foregroundImage: './assets/android-icon-foreground.png',
      monochromeImage: './assets/android-icon-monochrome.png',
    },
    predictiveBackGestureEnabled: false,
  },
  web: {
    favicon: './assets/favicon.png',
  },
  plugins: [
    'expo-router',
    'expo-secure-store',
    [
      'expo-location',
      {
        locationWhenInUsePermission:
          "Scootly, yakınındaki scooter'ları göstermek ve sürüşü bitirdiğin konumu kaydetmek için konumunu kullanır.",
        // Arka planda konum ve hareket verisi kullanılmıyor; gereksiz izin metinleri eklenmesin.
        locationAlwaysAndWhenInUsePermission: false,
        locationAlwaysPermission: false,
        motionUsagePermission: false,
      },
    ],
    [
      'expo-camera',
      {
        cameraPermission: "Scootly, scooter'ın QR kodunu okutup sürüşe başlaman için kamerayı kullanır.",
        // Yalnızca QR okunuyor; video ve ses kaydı yok.
        microphonePermission: false,
        recordAudioAndroid: false,
      },
    ],
    [
      'expo-splash-screen',
      {
        image: './assets/splash-icon.png',
        imageWidth: 160,
        resizeMode: 'contain',
        backgroundColor: BRAND_VOLT,
      },
    ],
    [
      'expo-build-properties',
      {
        android: {
          usesCleartextTraffic: allowHttp,
        },
        ios: {
          // iOS 27 SDK'sıyla derlenen uygulama UIScene yaşam döngüsünü benimsemezse açılışta kapanıyor.
          // SDK 58'in şablonunda hazır geliyor; SDK 57'de bu ayarla açılıyor (expo 57.0.23 ve sonrası).
          enableSceneSupport: true,
        },
      },
    ],
  ],
};

/**
 * Xcode'un betik kum havuzu (User Script Sandboxing), React Native'in "Bundle React Native code and images"
 * adımının proje dosyalarını okumasını engelliyor; iOS derlemesi bu adımda düşüyor. `ios/` üretilen bir
 * çıktı olduğundan ayar elle değil, her prebuild'de burada kapatılır.
 */
const withoutUserScriptSandboxing: ConfigPlugin = (expoConfig) =>
  withXcodeProject(expoConfig, (mod) => {
    const section = mod.modResults.pbxXCBuildConfigurationSection() as Record<string, unknown>;
    for (const entry of Object.values(section)) {
      if (typeof entry === 'object' && entry !== null && 'buildSettings' in entry) {
        (entry as { buildSettings: Record<string, unknown> }).buildSettings.ENABLE_USER_SCRIPT_SANDBOXING = 'NO';
      }
    }
    return mod;
  });

export default withoutUserScriptSandboxing(config);
