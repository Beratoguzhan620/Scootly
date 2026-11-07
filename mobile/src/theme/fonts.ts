import { Barlow_400Regular } from '@expo-google-fonts/barlow/400Regular';
import { Barlow_500Medium } from '@expo-google-fonts/barlow/500Medium';
import { Barlow_600SemiBold } from '@expo-google-fonts/barlow/600SemiBold';
import { Barlow_700Bold } from '@expo-google-fonts/barlow/700Bold';
import { BarlowCondensed_600SemiBold } from '@expo-google-fonts/barlow-condensed/600SemiBold';
import { BarlowCondensed_700Bold } from '@expo-google-fonts/barlow-condensed/700Bold';
import { BarlowCondensed_800ExtraBold_Italic } from '@expo-google-fonts/barlow-condensed/800ExtraBold_Italic';

import { fontFamily } from './typography';

/**
 * `useFonts` ile yüklenen dosyalar. Paketin kök dizininden değil kalınlık klasörlerinden alınır; kökten
 * alınırsa ailenin 18 kalınlığı da uygulamaya gömülür.
 */
export const fontAssets = {
  [fontFamily.regular]: Barlow_400Regular,
  [fontFamily.medium]: Barlow_500Medium,
  [fontFamily.semibold]: Barlow_600SemiBold,
  [fontFamily.bold]: Barlow_700Bold,
  [fontFamily.condensedSemibold]: BarlowCondensed_600SemiBold,
  [fontFamily.condensedBold]: BarlowCondensed_700Bold,
  [fontFamily.wordmark]: BarlowCondensed_800ExtraBold_Italic,
};
