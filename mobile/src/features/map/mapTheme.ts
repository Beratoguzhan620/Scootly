import { dark, INK, light, VOLT, type Scheme, type Theme } from '@/theme/palette';

/** Altlık stilinin yeniden boyanan katmanları (OpenMapTiles şeması). */
export type BaseMapColors = {
  land: string;
  residential: string;
  park: string;
  wood: string;
  water: string;
  building: string;
  buildingLine: string;
  road: string;
  roadMinor: string;
  roadCasing: string;
  motorway: string;
  path: string;
  rail: string;
  boundary: string;
  placeLabel: string;
  roadLabel: string;
  waterLabel: string;
  halo: string;
};

export type MapPalette = {
  base: BaseMapColors;
  /** Scooter işareti: sarı levha, lacivert batarya yayı */
  pin: {
    fill: string;
    ring: string;
    track: string;
    text: string;
    low: string;
    offFill: string;
    offText: string;
    border: string;
    shadow: string;
  };
  cluster: { fill: string; text: string; halo: string; border: string };
  user: { dot: string; border: string };
  area: { line: string; fill: string; fillOpacity: number; lineOpacity: number };
  /** Sayfa yüklenirken ve karolar gelmeden görünen zemin */
  background: string;
  /** Atıf kutusu */
  attribution: { bg: string; text: string; link: string; invertIcon: boolean };
};

function palette(theme: Theme, base: BaseMapColors): MapPalette {
  const isDark = theme.scheme === 'dark';
  return {
    base,
    pin: {
      fill: VOLT,
      ring: INK,
      track: isDark ? '#3A4A6E' : '#CBD2DF',
      text: INK,
      low: theme.critical.solid,
      offFill: isDark ? '#4A5878' : '#D5DAE3',
      offText: isDark ? '#E3E8F2' : '#55627F',
      border: '#FFFFFF',
      shadow: isDark ? 'rgba(0, 0, 0, 0.55)' : 'rgba(11, 30, 74, 0.35)',
    },
    cluster: isDark
      ? { fill: '#EEF2FA', text: INK, halo: 'rgba(238, 242, 250, 0.22)', border: '#0A1428' }
      : { fill: INK, text: '#FFFFFF', halo: 'rgba(11, 30, 74, 0.16)', border: '#FFFFFF' },
    user: { dot: theme.userDot, border: '#FFFFFF' },
    area: isDark
      ? { line: VOLT, fill: VOLT, fillOpacity: 0.04, lineOpacity: 0.55 }
      : { line: INK, fill: INK, fillOpacity: 0.035, lineOpacity: 0.5 },
    background: base.land,
    attribution: isDark
      ? { bg: 'rgba(19, 33, 61, 0.92)', text: theme.textMuted, link: theme.text, invertIcon: true }
      : { bg: 'rgba(255, 255, 255, 0.92)', text: theme.textMuted, link: theme.text, invertIcon: false },
  };
}

/** Açık tema: serin gri zemin, yumuşak mavi su, beyaz yollar; otoyollar sarının soluk tonu. */
const lightBase: BaseMapColors = {
  land: '#EDF0F5',
  residential: '#E7EBF1',
  park: '#DCEBDD',
  wood: '#D3E5D5',
  water: '#BFD4EE',
  building: '#E0E5EE',
  buildingLine: '#D2D9E5',
  road: '#FFFFFF',
  roadMinor: '#FFFFFF',
  roadCasing: '#D3DAE6',
  motorway: '#FFEDA0',
  path: '#DCE2EC',
  rail: '#CAD2DF',
  boundary: '#A7B1C4',
  placeLabel: '#22325A',
  roadLabel: '#5D6A88',
  waterLabel: '#3F6296',
  halo: '#F3F5F9',
};

/** Koyu tema: lacivert gece; yollar zeminden biraz açık, etiketler soluk mavi-gri. */
const darkBase: BaseMapColors = {
  land: '#0F1A2F',
  residential: '#111E36',
  park: '#11283A',
  wood: '#122D3D',
  water: '#08111F',
  building: '#16233D',
  buildingLine: '#1C2B49',
  road: '#25365A',
  roadMinor: '#1C2B48',
  roadCasing: '#0F1A2F',
  motorway: '#4A4322',
  path: '#1A2742',
  rail: '#24334F',
  boundary: '#3A4A6E',
  placeLabel: '#B4C0DA',
  roadLabel: '#8391B0',
  waterLabel: '#5F7DB0',
  halo: '#0F1A2F',
};

export const mapPalettes: Record<Scheme, MapPalette> = {
  light: palette(light, lightBase),
  dark: palette(dark, darkBase),
};
