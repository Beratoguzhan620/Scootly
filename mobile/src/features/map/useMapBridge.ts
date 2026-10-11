import { useEffect, useMemo, useRef, useState } from 'react';

import { centerCommand, parseMapEvent, toStateCommand, zoomForRegion, type MapCommand } from './mapBridge';
import { FOCUS_ZOOM, isAllowedExternalUrl, MAP_ATTRIBUTION, MAP_TILE_URL } from './mapConfig';
import { buildMapHtml } from './mapHtml';
import type { VehicleMapHandle, VehicleMapProps } from './types';
import type { LatLng } from '@/lib/geo';

type Platform = {
  /** Komutu sayfaya iletir (WebView'a enjekte eder ya da iframe'e postMessage). Kimliği sabit olmalı. */
  send: (command: MapCommand) => void;
  /** İzin verilen dış bağlantıyı sistem tarayıcısında açar. */
  openUrl: (url: string) => void;
};

/**
 * Harita sayfasıyla konuşan ortak mantık; WebView (iOS/Android) ve iframe (web) kabukları bunu kullanır.
 * Sayfa hazır olunca durum gönderilir, hazır olmadan istenen odak beklemeye alınır.
 */
export function useMapBridge(props: VehicleMapProps, { send, openUrl }: Platform) {
  const { vehicles, serviceAreas, selectedId, initialRegion, userLocation, bottomInset, onSelect, onRegionChange } = props;
  const pendingCenter = useRef<LatLng | null>(null);
  const [ready, setReady] = useState(false);

  // Sayfa bir kez üretilir; sonraki değişiklikler mesajla gider (kaynak değişirse sayfa baştan yüklenir).
  const [html] = useState(() =>
    buildMapHtml({
      center: [initialRegion.latitude, initialRegion.longitude],
      zoom: zoomForRegion(initialRegion),
      focusZoom: FOCUS_ZOOM,
      tileUrl: MAP_TILE_URL,
      attribution: MAP_ATTRIBUTION,
    }),
  );

  const stateCommand = useMemo(
    () => toStateCommand({ vehicles, serviceAreas, selectedId, userLocation, bottomInset }),
    [vehicles, serviceAreas, selectedId, userLocation, bottomInset],
  );

  useEffect(() => {
    if (ready) send(stateCommand);
  }, [ready, stateCommand, send]);

  useEffect(() => {
    if (!ready || !pendingCenter.current) return;
    send(centerCommand(pendingCenter.current));
    pendingCenter.current = null;
  }, [ready, send]);

  const handle = useMemo<VehicleMapHandle>(
    () => ({
      centerOn: (point) => {
        if (ready) send(centerCommand(point));
        else pendingCenter.current = point;
      },
    }),
    [ready, send],
  );

  /** Sayfadan gelen ham mesajı işler. */
  const handleMessage = (data: unknown) => {
    const message = parseMapEvent(data);
    if (!message) return;

    switch (message.type) {
      case 'ready':
        setReady(true);
        break;
      case 'region':
        onRegionChange(message.region);
        break;
      case 'select':
        onSelect(message.id);
        break;
      case 'mapPress':
        onSelect(null);
        break;
      case 'openUrl':
        if (isAllowedExternalUrl(message.url)) openUrl(message.url);
        break;
      case 'error':
        if (__DEV__) console.warn('[harita]', message.message);
        break;
    }
  };

  /** Sayfa yeniden yüklenmeye başlayınca: hazır olana kadar komut gönderilmez. */
  const reset = () => setReady(false);

  return { html, handle, handleMessage, reset };
}
