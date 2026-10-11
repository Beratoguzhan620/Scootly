import { useCallback, useImperativeHandle, useRef, type Ref } from 'react';
import { Linking, StyleSheet, View } from 'react-native';
import { WebView } from 'react-native-webview';

import type { MapCommand } from './mapBridge';
import { MAP_PAGE_URL } from './mapConfig';
import type { VehicleMapHandle, VehicleMapProps } from './types';
import { useMapBridge } from './useMapBridge';
import { appConfig } from '@/config';
import { colors } from '@/theme';

export type { VehicleMapHandle };

/**
 * iOS ve Android haritası: paneldeki gibi Leaflet + OpenStreetMap, bir WebView içinde. Google ya da Apple
 * harita anahtarı gerektirmez; Expo Go'da ve kendi derlemelerimizde aynı çalışır.
 */
export function VehicleMap({ ref, ...props }: VehicleMapProps & { ref?: Ref<VehicleMapHandle> }) {
  const webViewRef = useRef<WebView>(null);

  const send = useCallback((command: MapCommand) => {
    const payload = JSON.stringify(JSON.stringify(command));
    webViewRef.current?.injectJavaScript(`window.__scootly && window.__scootly.receive(${payload}); true;`);
  }, []);
  const openUrl = useCallback((url: string) => void Linking.openURL(url), []);

  const { html, handle, handleMessage, reset } = useMapBridge(props, { send, openUrl });
  useImperativeHandle(ref, () => handle, [handle]);

  return (
    <View style={StyleSheet.absoluteFill}>
      <WebView
        ref={webViewRef}
        source={{ html, baseUrl: MAP_PAGE_URL }}
        originWhitelist={['*']}
        onMessage={(event) => handleMessage(event.nativeEvent.data)}
        // Sayfa dışına gezinme yok; atıf bağlantıları sistem tarayıcısında açılır.
        onShouldStartLoadWithRequest={(request) =>
          request.url === 'about:blank' || request.url.startsWith(MAP_PAGE_URL) || request.url.startsWith('data:')
        }
        setSupportMultipleWindows={false}
        javaScriptEnabled
        domStorageEnabled={false}
        scrollEnabled={false}
        bounces={false}
        overScrollMode="never"
        showsHorizontalScrollIndicator={false}
        showsVerticalScrollIndicator={false}
        allowsLinkPreview={false}
        textInteractionEnabled={false}
        // Karo sunucuları uygulamayı User-Agent'tan tanır (OSM kullanım kuralı).
        applicationNameForUserAgent={`Scootly/${appConfig.version}`}
        webviewDebuggingEnabled={__DEV__}
        onLoadStart={reset}
        // Bellek baskısıyla WebView süreci kapanırsa haritayı yeniden yükle.
        onContentProcessDidTerminate={() => webViewRef.current?.reload()}
        onRenderProcessGone={() => webViewRef.current?.reload()}
        style={styles.webView}
        containerStyle={styles.webView}
      />
    </View>
  );
}

const styles = StyleSheet.create({
  webView: {
    flex: 1,
    backgroundColor: colors.secondaryBg,
  },
});
