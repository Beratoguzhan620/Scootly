import { useSyncExternalStore } from 'react';

import { appConfig } from '@/config';
import { storage } from '@/lib/storage';
import { normalizeApiUrl } from '@/lib/url';

const STORAGE_KEY = 'scootly.apiBaseUrl';

/**
 * Uygulamanın konuştuğu API adresi. Varsayılanı derleme anında belirlenir (`EXPO_PUBLIC_API_URL`); test
 * sürümlerinde kullanıcı giriş ekranındaki "Sunucu ayarları"ndan değiştirebilir ve seçimi cihazda saklanır.
 * Böylece bilgisayarın yerel IP'si değişince uygulamayı yeniden derlemek gerekmez.
 */
let current = appConfig.apiBaseUrl;
let custom = false;
const listeners = new Set<() => void>();

function emit() {
  listeners.forEach((listener) => listener());
}

export const serverStore = {
  get: (): string => current,
  isCustom: (): boolean => custom,
  defaultUrl: (): string => appConfig.apiBaseUrl,

  /** Kayıtlı adresi yükler; uygulama açılırken ilk istekten önce çağrılır. */
  async load(): Promise<void> {
    if (!appConfig.serverEditable) return;
    const saved = normalizeApiUrl((await storage.getItem(STORAGE_KEY)) ?? '');
    if (saved) {
      current = saved;
      custom = saved !== appConfig.apiBaseUrl;
      emit();
    }
  },

  /** `null`: derlemedeki varsayılana dön. */
  async set(url: string | null): Promise<void> {
    if (url === null || url === appConfig.apiBaseUrl) {
      current = appConfig.apiBaseUrl;
      custom = false;
      await storage.removeItem(STORAGE_KEY);
    } else {
      current = url;
      custom = true;
      await storage.setItem(STORAGE_KEY, url);
    }
    emit();
  },

  subscribe(listener: () => void): () => void {
    listeners.add(listener);
    return () => listeners.delete(listener);
  },
};

/** Güncel API adresi; değişince bileşeni yeniden çizer. */
export function useApiBaseUrl(): string {
  return useSyncExternalStore(serverStore.subscribe, serverStore.get, serverStore.get);
}

export type ServerCheck = {
  ok: boolean;
  tone: 'success' | 'warning' | 'danger';
  message: string;
};

/**
 * Adresin gerçekten Scootly API'si olduğunu ve hazır olduğunu denetler (`/health/ready`, oturum gerektirmez).
 * Hazır değilse ASP.NET sağlık kontrolü 503 döner.
 */
export async function checkServer(baseUrl: string, timeoutMs = 6_000, fetchImpl: typeof fetch = fetch): Promise<ServerCheck> {
  const controller = new AbortController();
  let timedOut = false;
  const timer = setTimeout(() => {
    timedOut = true;
    controller.abort();
  }, timeoutMs);

  try {
    const response = await fetchImpl(`${baseUrl}/health/ready`, { signal: controller.signal });
    if (response.ok) return { ok: true, tone: 'success', message: 'Bağlantı başarılı: Scootly API çalışıyor ve hazır.' };
    if (response.status === 503) {
      return {
        ok: false,
        tone: 'warning',
        message: 'API çalışıyor ama hazır değil. Veritabanı ya da mesaj kuyruğu henüz ayağa kalkmamış olabilir.',
      };
    }
    if (response.status === 404) {
      return {
        ok: false,
        tone: 'danger',
        message: "Bu adreste Scootly API'si bulunamadı. Portu kontrol edin: web panelinin değil, API'nin portu olmalı.",
      };
    }
    return { ok: false, tone: 'danger', message: `Sunucu beklenmeyen bir yanıt verdi (HTTP ${response.status}).` };
  } catch {
    return {
      ok: false,
      tone: 'danger',
      message: timedOut
        ? 'Sunucu zamanında yanıt vermedi. Adresi ve telefonun bilgisayarla aynı ağda olduğunu kontrol edin.'
        : "Sunucuya ulaşılamadı. Telefonun ve bilgisayarın aynı Wi-Fi ağında olduğunu, API'nin yerel ağa açık olduğunu ve adresin doğru yazıldığını kontrol edin.",
    };
  } finally {
    clearTimeout(timer);
  }
}
