import { CameraView, useCameraPermissions, type BarcodeScanningResult } from 'expo-camera';
import { useRouter } from 'expo-router';
import { StatusBar } from 'expo-status-bar';
import { useEffect, useRef, useState } from 'react';
import { KeyboardAvoidingView, Linking, Platform, Pressable, StyleSheet, Text, View } from 'react-native';
import { useSafeAreaInsets } from 'react-native-safe-area-context';

import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Icon } from '@/components/Icon';
import { Plate } from '@/components/Plate';
import { TextField } from '@/components/TextField';
import { normalizeCodeInput, parseVehicleCode, VEHICLE_CODE_LENGTH, type ScannedVehicle } from '@/domain/vehicleCode';
import { haptics } from '@/lib/haptics';
import { useReservation } from '@/ride/ReservationProvider';
import { INK, makeStyles, radius, space, textVariants, VOLT } from '@/theme';

const FRAME = 248;
/** Aynı yanlış kod kamerada dururken uyarı tekrar tekrar titreşmesin. */
const REJECT_COOLDOWN_MS = 2_500;

/** Scooter'ın QR kodunu okutma; okunamazsa plakadaki 8 haneli kod elle girilir. */
export default function ScanScreen() {
  const styles = useStyles();
  const router = useRouter();
  const insets = useSafeAreaInsets();
  const { reservation } = useReservation();
  const [permission, requestPermission] = useCameraPermissions();
  const [torch, setTorch] = useState(false);
  const [manual, setManual] = useState(false);
  const [code, setCode] = useState('');
  const [codeError, setCodeError] = useState<string | null>(null);
  const [rejected, setRejected] = useState(false);
  const [cameraError, setCameraError] = useState(false);
  const handled = useRef(false);
  const lastRejected = useRef({ data: '', at: 0 });

  // İzin ilk kez soruluyorsa ekran açılır açılmaz sor.
  useEffect(() => {
    if (permission && !permission.granted && permission.canAskAgain && permission.status === 'undetermined') {
      void requestPermission();
    }
  }, [permission, requestPermission]);

  useEffect(() => {
    if (!rejected) return;
    const timer = setTimeout(() => setRejected(false), REJECT_COOLDOWN_MS);
    return () => clearTimeout(timer);
  }, [rejected]);

  const open = (target: ScannedVehicle) => {
    handled.current = true;
    haptics.success();
    router.replace({
      pathname: '/start-ride',
      params: target.kind === 'id' ? { vehicleId: target.id } : { code: target.code },
    });
  };

  const onScanned = ({ data }: BarcodeScanningResult) => {
    if (handled.current) return;
    const target = parseVehicleCode(data);
    if (target) {
      open(target);
      return;
    }
    const now = Date.now();
    if (lastRejected.current.data === data && now - lastRejected.current.at < REJECT_COOLDOWN_MS) return;
    lastRejected.current = { data, at: now };
    haptics.warning();
    setRejected(true);
  };

  const submitCode = () => {
    const target = parseVehicleCode(code);
    if (!target) {
      setCodeError(`Kod ${VEHICLE_CODE_LENGTH} karakter olmalı (0-9 ve A-F).`);
      return;
    }
    open(target);
  };

  const granted = permission?.granted === true && !cameraError;
  const denied = permission !== null && !permission.granted && !permission.canAskAgain;

  let cameraArea;
  if (granted) {
    cameraArea = (
      <>
        <CameraView
          style={StyleSheet.absoluteFill}
          facing="back"
          enableTorch={torch}
          barcodeScannerSettings={{ barcodeTypes: ['qr'] }}
          onBarcodeScanned={onScanned}
          onMountError={() => setCameraError(true)}
        />
        <View style={styles.dimmer} pointerEvents="none">
          <View style={styles.dim} />
          <View style={styles.frameRow}>
            <View style={styles.dim} />
            <View style={styles.frame}>
              <View style={[styles.corner, styles.topLeft]} />
              <View style={[styles.corner, styles.topRight]} />
              <View style={[styles.corner, styles.bottomLeft]} />
              <View style={[styles.corner, styles.bottomRight]} />
            </View>
            <View style={styles.dim} />
          </View>
          <View style={[styles.dim, styles.dimBottom]}>
            <Text style={styles.cameraHint}>
              {rejected ? 'Bu bir Scootly kodu değil.' : "Scooter'ın gidonundaki QR kodu çerçeveye getir."}
            </Text>
          </View>
        </View>
      </>
    );
  } else {
    cameraArea = (
      <View style={styles.noCamera}>
        <View style={styles.noCameraIcon}>
          <Icon name="camera-outline" size={30} color={INK} />
        </View>
        <Text style={styles.noCameraTitle}>
          {cameraError ? 'Kamera açılamadı' : denied ? 'Kamera izni kapalı' : 'Kamera izni gerekiyor'}
        </Text>
        <Text style={styles.noCameraText}>
          {cameraError
            ? 'Bu cihazda kamera kullanılamıyor. Scooter kodunu elle girebilirsin.'
            : "QR kodu okutmak için Scootly'nin kameraya erişmesi gerekiyor. İstersen kodu elle de girebilirsin."}
        </Text>
        {cameraError ? null : denied ? (
          <Button
            title="Ayarları aç"
            inline
            style={styles.centered}
            onPress={() => void Linking.openSettings()}
          />
        ) : permission ? (
          <Button title="Kameraya izin ver" inline style={styles.centered} onPress={() => void requestPermission()} />
        ) : null}
      </View>
    );
  }

  return (
    <KeyboardAvoidingView style={styles.root} behavior={Platform.OS === 'ios' ? 'padding' : undefined}>
      <StatusBar style="light" />
      <View style={styles.camera}>
        {cameraArea}
        <View style={[styles.topBar, { paddingTop: insets.top + space.sm }]}>
          <Pressable
            accessibilityRole="button"
            accessibilityLabel="Kapat"
            hitSlop={8}
            onPress={() => router.back()}
            style={({ pressed }) => [styles.roundButton, pressed && styles.roundPressed]}>
            <Icon name="close" size={24} color="#FFFFFF" />
          </Pressable>
          <Text style={styles.title} accessibilityRole="header">
            QR kodu okut
          </Text>
          {granted ? (
            <Pressable
              accessibilityRole="switch"
              accessibilityLabel="Fener"
              accessibilityState={{ checked: torch }}
              hitSlop={8}
              onPress={() => setTorch((value) => !value)}
              style={({ pressed }) => [styles.roundButton, torch && styles.roundActive, pressed && styles.roundPressed]}>
              <Icon name={torch ? 'flashlight' : 'flashlight-outline'} size={22} color={torch ? INK : '#FFFFFF'} />
            </Pressable>
          ) : (
            <View style={styles.roundSpacer} />
          )}
        </View>
        {reservation ? (
          <View style={styles.reservedChip} accessible accessibilityLabel="Rezerve ettiğin scooter">
            <Text style={styles.reservedText}>Rezervasyonun</Text>
            <Plate id={reservation.vehicleId} kind="Araç" inverse />
          </View>
        ) : null}
      </View>

      <View style={[styles.panel, { paddingBottom: insets.bottom + space.lg }]}>
        {manual ? (
          <>
            <TextField
              label="Scooter kodu"
              hint="Kod, scooter'ın plakasında ve QR kodun altında yazar."
              error={codeError}
              value={code}
              onChangeText={(value) => {
                setCode(normalizeCodeInput(value));
                setCodeError(null);
              }}
              placeholder="3F2B8C1A"
              autoCapitalize="characters"
              autoCorrect={false}
              autoFocus
              maxLength={VEHICLE_CODE_LENGTH}
              returnKeyType="go"
              onSubmitEditing={submitCode}
              style={styles.codeInput}
            />
            <Button title="Devam" size="lg" icon="arrow-forward" onPress={submitCode} />
          </>
        ) : (
          <>
            <AppText variant="heading">Kod okunmuyor mu?</AppText>
            <AppText muted>QR kodu yıpranmışsa scooter&apos;ın plakasındaki 8 karakterlik kodu yazabilirsin.</AppText>
            <Button
              title="Kodu elle gir"
              variant="secondary"
              icon="keypad-outline"
              onPress={() => {
                setManual(true);
                setTorch(false);
              }}
            />
          </>
        )}
      </View>
    </KeyboardAvoidingView>
  );
}

const CORNER = 34;
const STROKE = 5;

const useStyles = makeStyles((t) => ({
  root: {
    flex: 1,
    backgroundColor: '#05080F',
  },
  camera: {
    flex: 1,
    overflow: 'hidden',
    backgroundColor: '#05080F',
  },
  dimmer: {
    position: 'absolute',
    top: 0,
    right: 0,
    bottom: 0,
    left: 0,
  },
  dim: {
    flex: 1,
    backgroundColor: 'rgba(5, 8, 15, 0.55)',
  },
  dimBottom: {
    alignItems: 'center',
    paddingTop: space.xl,
    paddingHorizontal: space.xxl,
  },
  frameRow: {
    flexDirection: 'row',
    height: FRAME,
  },
  frame: {
    width: FRAME,
    height: FRAME,
  },
  corner: {
    position: 'absolute',
    width: CORNER,
    height: CORNER,
    borderColor: VOLT,
  },
  topLeft: {
    top: 0,
    left: 0,
    borderTopWidth: STROKE,
    borderLeftWidth: STROKE,
    borderTopLeftRadius: radius.control,
  },
  topRight: {
    top: 0,
    right: 0,
    borderTopWidth: STROKE,
    borderRightWidth: STROKE,
    borderTopRightRadius: radius.control,
  },
  bottomLeft: {
    bottom: 0,
    left: 0,
    borderBottomWidth: STROKE,
    borderLeftWidth: STROKE,
    borderBottomLeftRadius: radius.control,
  },
  bottomRight: {
    bottom: 0,
    right: 0,
    borderBottomWidth: STROKE,
    borderRightWidth: STROKE,
    borderBottomRightRadius: radius.control,
  },
  cameraHint: {
    ...textVariants.bodyStrong,
    color: '#FFFFFF',
    textAlign: 'center',
  },
  topBar: {
    position: 'absolute',
    top: 0,
    left: 0,
    right: 0,
    flexDirection: 'row',
    alignItems: 'center',
    justifyContent: 'space-between',
    paddingHorizontal: space.lg,
  },
  title: {
    ...textVariants.heading,
    color: '#FFFFFF',
  },
  roundButton: {
    width: 44,
    height: 44,
    borderRadius: 22,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: 'rgba(255, 255, 255, 0.16)',
  },
  roundActive: {
    backgroundColor: VOLT,
  },
  roundPressed: {
    opacity: 0.7,
  },
  roundSpacer: {
    width: 44,
  },
  reservedChip: {
    position: 'absolute',
    top: 0,
    alignSelf: 'center',
    marginTop: 112,
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.sm,
    backgroundColor: 'rgba(11, 30, 74, 0.85)',
    borderRadius: radius.pill,
    paddingVertical: space.xs + 2,
    paddingLeft: space.md + 2,
    paddingRight: space.sm,
  },
  reservedText: {
    ...textVariants.captionStrong,
    color: '#FFFFFF',
  },
  noCamera: {
    flex: 1,
    alignItems: 'center',
    justifyContent: 'center',
    gap: space.md,
    paddingHorizontal: space.xxxl,
  },
  centered: {
    alignSelf: 'center',
  },
  noCameraIcon: {
    width: 64,
    height: 64,
    borderRadius: 32,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: VOLT,
    marginBottom: space.xs,
  },
  noCameraTitle: {
    ...textVariants.title,
    color: '#FFFFFF',
    textAlign: 'center',
  },
  noCameraText: {
    ...textVariants.body,
    color: '#C7CFDF',
    textAlign: 'center',
    marginBottom: space.sm,
  },
  panel: {
    gap: space.md,
    backgroundColor: t.surface,
    borderTopLeftRadius: radius.sheet,
    borderTopRightRadius: radius.sheet,
    paddingTop: space.xl,
    paddingHorizontal: space.xl,
    marginTop: -radius.sheet,
  },
  codeInput: {
    ...textVariants.numeral,
    fontSize: 28,
    lineHeight: 34,
    letterSpacing: 3,
  },
}));
