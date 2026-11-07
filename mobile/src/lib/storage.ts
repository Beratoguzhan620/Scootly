import * as SecureStore from 'expo-secure-store';

/**
 * Cihazda şifreli saklama (iOS Keychain, Android Keystore). Token ve küçük durum kayıtları için.
 * Web önizlemesi `storage.web.ts`'yi kullanır.
 */
export const storage = {
  getItem: (key: string) => SecureStore.getItemAsync(key),
  setItem: (key: string, value: string) => SecureStore.setItemAsync(key, value),
  removeItem: (key: string) => SecureStore.deleteItemAsync(key),
};
