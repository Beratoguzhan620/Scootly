import type { ExpoConfig } from 'expo/config';

/** Marka sarısı (voltaj sarısı); simge, açılış ekranı ve uygulama içi açılış görünümü aynı zemini kullanır. */
const BRAND_VOLT = '#FFD60A';

/**
 * Release derlemede API'ye düz HTTP ile bağlanılabilsin mi? Test sürümlerinde API, bilgisayarın yerel ağ
 * adresinde HTTP ile çalışıyor; Android 9+ ise release derlemelerde HTTP'yi varsayılan olarak engeller.
 * Mağaza sürümünde API HTTPS ile yayınlanır ve derleme `SCOOTLY_ALLOW_HTTP=false` ile alınır.
 */
const allowHttp = process.env.SCOOTLY_ALLOW_HTTP !== 'false';

/** Her CI derlemesi bir öncekinin üstüne kurulabilsin diye artan derleme numarası. */
const versionCode = Number(process.env.SCOOTLY_VERSION_CODE) || 1;

const config: ExpoConfig = {
  name: 'Scootly',
  slug: 'scootly-mobile',
  scheme: 'scootly',
  version: '0.1.0',
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
      },
    ],
  ],
};

export default config;
