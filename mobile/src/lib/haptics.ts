import * as Haptics from 'expo-haptics';
import { Platform } from 'react-native';

const supported = Platform.OS === 'ios' || Platform.OS === 'android';

function run(feedback: () => Promise<void>) {
  if (!supported) return;
  // Titreşim motoru olmayan ya da titreşimi kapalı cihazlarda sessizce geçilir.
  feedback().catch(() => undefined);
}

/** Dokunsal geri bildirim: yalnızca bir eylemin sonucunu hissettirmek için, her dokunuşta değil. */
export const haptics = {
  /** Haritada bir scooter seçmek, paneli sürüklemek gibi küçük seçimler */
  select: () => run(() => Haptics.selectionAsync()),
  /** Sürüşü başlatmak gibi belirgin bir adım */
  impact: () => run(() => Haptics.impactAsync(Haptics.ImpactFeedbackStyle.Medium)),
  success: () => run(() => Haptics.notificationAsync(Haptics.NotificationFeedbackType.Success)),
  warning: () => run(() => Haptics.notificationAsync(Haptics.NotificationFeedbackType.Warning)),
  error: () => run(() => Haptics.notificationAsync(Haptics.NotificationFeedbackType.Error)),
};
