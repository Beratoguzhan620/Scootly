import { useImperativeHandle, type Ref } from 'react';
import { ScrollView, StyleSheet, Text, View } from 'react-native';

import type { VehicleMapHandle, VehicleMapProps } from './types';
import { VehicleMarker } from './VehicleMarker';
import { AlertBox } from '@/components/AlertBox';
import { Code } from '@/components/AppText';
import { ListGroup, ListGroupItem } from '@/components/ListGroup';
import { shortId } from '@/lib/format';
import { colors, spacing, typography } from '@/theme';

export type { VehicleMapHandle } from './types';

/**
 * Web önizlemesi: react-native-maps web'de çalışmadığı için araçları liste olarak gösterir. Yalnızca
 * geliştirme sırasında akışları tarayıcıda denemek içindir; asıl hedef iOS ve Android.
 */
export function VehicleMap({
  vehicles,
  selectedId,
  bottomInset,
  onSelect,
  ref,
}: VehicleMapProps & { ref?: Ref<VehicleMapHandle> }) {
  useImperativeHandle(ref, () => ({ centerOn: () => {} }), []);

  return (
    <View style={[StyleSheet.absoluteFill, styles.root]}>
      <ScrollView contentContainerStyle={[styles.content, { paddingBottom: bottomInset + spacing[3] }]}>
        <AlertBox tone="info" style={styles.notice}>
          Harita web önizlemesinde gösterilmiyor; araçlar liste halinde.
        </AlertBox>
        {vehicles.length > 0 ? (
          <ListGroup>
            {vehicles.map((vehicle) => (
              <ListGroupItem
                key={vehicle.id}
                onPress={() => onSelect(vehicle.id === selectedId ? null : vehicle.id)}
                accessibilityLabel={`${vehicle.brand} seç`}>
                <View style={styles.row}>
                  <VehicleMarker
                    status={vehicle.status}
                    battery={vehicle.batteryPercentage}
                    selected={vehicle.id === selectedId}
                  />
                  <Text style={styles.brand}>{vehicle.brand}</Text>
                  <Code>{shortId(vehicle.id)}</Code>
                </View>
              </ListGroupItem>
            ))}
          </ListGroup>
        ) : null}
      </ScrollView>
    </View>
  );
}

const styles = StyleSheet.create({
  root: {
    backgroundColor: colors.tertiaryBg,
  },
  content: {
    padding: spacing[3],
    gap: spacing[3],
  },
  notice: {
    // Sağ üstteki harita düğmelerinin altında kalmasın.
    marginRight: 52,
  },
  row: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: spacing[3],
  },
  brand: {
    ...typography.body,
    color: colors.body,
    flex: 1,
  },
});
