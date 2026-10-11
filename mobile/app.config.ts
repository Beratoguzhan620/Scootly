import type { ExpoConfig } from 'expo/config';

// Android geliştirme/üretim derlemelerinde Google Haritalar için gerekir; Expo Go'da gerekmez.
// Anahtar repoya girmesin diye ortam değişkeninden okunur (bkz. README → Harita anahtarı).
const googleMapsAndroidApiKey = process.env.GOOGLE_MAPS_ANDROID_API_KEY;

const config: ExpoConfig = {
  name: 'Scootly',
  slug: 'scootly-mobile',
  scheme: 'scootly',
  version: '0.1.0',
  orientation: 'portrait',
  icon: './assets/icon.png',
  // Panel yalnızca açık temayı kullanıyor; mobil de aynı görünümle başlıyor.
  userInterfaceStyle: 'light',
  ios: {
    supportsTablet: true,
    bundleIdentifier: 'com.scootly.mobile',
  },
  android: {
    package: 'com.scootly.mobile',
    adaptiveIcon: {
      backgroundColor: '#E6F4FE',
      foregroundImage: './assets/android-icon-foreground.png',
      backgroundImage: './assets/android-icon-background.png',
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
    ['react-native-maps', googleMapsAndroidApiKey ? { androidGoogleMapsApiKey: googleMapsAndroidApiKey } : {}],
  ],
};

export default config;
