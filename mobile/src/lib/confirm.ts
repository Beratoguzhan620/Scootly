import { Alert, Platform } from 'react-native';

type ConfirmOptions = {
  title: string;
  message: string;
  confirmLabel: string;
  cancelLabel?: string;
  destructive?: boolean;
};

/** Sistem onay penceresi. React Native Web'de `Alert.alert` düğme desteklemediği için `window.confirm`. */
export function confirm({ title, message, confirmLabel, cancelLabel = 'Vazgeç', destructive }: ConfirmOptions) {
  return new Promise<boolean>((resolve) => {
    if (Platform.OS === 'web') {
      resolve(typeof window !== 'undefined' && window.confirm(`${title}\n\n${message}`));
      return;
    }
    Alert.alert(
      title,
      message,
      [
        { text: cancelLabel, style: 'cancel', onPress: () => resolve(false) },
        { text: confirmLabel, style: destructive ? 'destructive' : 'default', onPress: () => resolve(true) },
      ],
      { cancelable: true, onDismiss: () => resolve(false) },
    );
  });
}
