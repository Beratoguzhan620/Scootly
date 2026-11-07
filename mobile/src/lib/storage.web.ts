/**
 * Web önizlemesi için yedek: SecureStore web'de yok. Tarayıcı depolaması şifreli değildir;
 * web sürümü yalnızca geliştirme sırasında arayüze bakmak içindir.
 */
function local(): Storage | null {
  try {
    return typeof window !== 'undefined' ? window.localStorage : null;
  } catch {
    return null;
  }
}

export const storage = {
  getItem: async (key: string) => {
    try {
      return local()?.getItem(key) ?? null;
    } catch {
      return null;
    }
  },
  setItem: async (key: string, value: string) => {
    try {
      local()?.setItem(key, value);
    } catch {
      // Gizli pencere ya da kapalı depolama: oturum yalnızca bellekte kalır.
    }
  },
  removeItem: async (key: string) => {
    try {
      local()?.removeItem(key);
    } catch {
      // yok say
    }
  },
};
