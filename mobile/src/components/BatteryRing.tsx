import { StyleSheet, Text, View } from 'react-native';
import Svg, { Circle } from 'react-native-svg';

import { batteryLevel, batteryPercent } from '@/domain/battery';
import { fontFamily, useTheme } from '@/theme';

type Props = {
  percentage: number;
  size?: number;
  /** Lacivert sürüş panelinde */
  inverse?: boolean;
};

/**
 * Batarya halkası: haritadaki işaretlerle aynı dil. Yay, kalan şarj kadar dolar; düşük şarjda kırmızı.
 */
export function BatteryRing({ percentage, size = 56, inverse = false }: Props) {
  const theme = useTheme();
  const value = batteryPercent(percentage);
  const level = batteryLevel(value);
  const stroke = Math.max(4, Math.round(size / 11));
  const r = (size - stroke) / 2;
  const circumference = 2 * Math.PI * r;
  const center = size / 2;

  const arcColor = level === 'ok' ? (inverse ? theme.ride.accent : theme.text) : theme.critical.solid;
  const trackColor = inverse ? theme.ride.line : theme.surfaceAlt;
  const textColor = inverse ? theme.ride.text : level === 'unrentable' ? theme.critical.fg : theme.text;
  const numberSize = Math.round(size * 0.36);

  return (
    <View
      style={{ width: size, height: size }}
      accessible
      accessibilityRole="image"
      accessibilityLabel={`Batarya yüzde ${value}`}>
      <Svg width={size} height={size}>
        <Circle cx={center} cy={center} r={r} stroke={trackColor} strokeWidth={stroke} fill="none" />
        {value > 0 ? (
          <Circle
            cx={center}
            cy={center}
            r={r}
            stroke={arcColor}
            strokeWidth={stroke}
            fill="none"
            strokeLinecap="round"
            strokeDasharray={`${circumference} ${circumference}`}
            strokeDashoffset={circumference * (1 - value / 100)}
            transform={`rotate(-90 ${center} ${center})`}
          />
        ) : null}
      </Svg>
      <View style={[StyleSheet.absoluteFill, styles.center]}>
        <Text style={[styles.number, { color: textColor, fontSize: numberSize, lineHeight: numberSize + 2 }]}>
          <Text style={{ fontSize: Math.round(numberSize * 0.6) }}>%</Text>
          {value}
        </Text>
      </View>
    </View>
  );
}

const styles = StyleSheet.create({
  center: {
    alignItems: 'center',
    justifyContent: 'center',
  },
  number: {
    fontFamily: fontFamily.condensedBold,
    fontVariant: ['tabular-nums'],
  },
});
