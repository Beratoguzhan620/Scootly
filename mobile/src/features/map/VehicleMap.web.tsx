import { useCallback, useEffect, useImperativeHandle, useRef, type Ref } from 'react';
import { StyleSheet, View } from 'react-native';

import type { MapCommand } from './mapBridge';
import type { VehicleMapHandle, VehicleMapProps } from './types';
import { useMapBridge } from './useMapBridge';
import { colors } from '@/theme';

export type { VehicleMapHandle };

/**
 * Web önizlemesi: telefondaki haritayla aynı sayfa, WebView yerine yalıtılmış (sandbox) bir iframe içinde.
 * Mesajlaşma `postMessage` ile; sayfadaki betik iki ortamı da tanır.
 */
export function VehicleMap({ ref, ...props }: VehicleMapProps & { ref?: Ref<VehicleMapHandle> }) {
  const frameRef = useRef<HTMLIFrameElement>(null);

  const send = useCallback((command: MapCommand) => {
    frameRef.current?.contentWindow?.postMessage(JSON.stringify(command), '*');
  }, []);
  const openUrl = useCallback((url: string) => void window.open(url, '_blank', 'noopener'), []);

  const { html, handle, handleMessage } = useMapBridge(props, { send, openUrl });
  useImperativeHandle(ref, () => handle, [handle]);

  // Dinleyici bir kez eklenir; mesajlar her zaman en güncel işleyiciye gider.
  const handlerRef = useRef(handleMessage);
  useEffect(() => {
    handlerRef.current = handleMessage;
  });
  useEffect(() => {
    const listener = (event: MessageEvent) => {
      if (event.source === frameRef.current?.contentWindow) handlerRef.current(event.data);
    };
    window.addEventListener('message', listener);
    return () => window.removeEventListener('message', listener);
  }, []);

  return (
    <View style={[StyleSheet.absoluteFill, styles.root]}>
      <iframe ref={frameRef} srcDoc={html} sandbox="allow-scripts" title="Araç haritası" style={frameStyle} />
    </View>
  );
}

const frameStyle = { border: 0, width: '100%', height: '100%', display: 'block' } as const;

const styles = StyleSheet.create({
  root: {
    backgroundColor: colors.secondaryBg,
  },
});
