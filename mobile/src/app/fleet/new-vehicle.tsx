import { useMutation, useQueryClient } from '@tanstack/react-query';
import { useLocalSearchParams, useRouter } from 'expo-router';
import { useState } from 'react';
import { Pressable, Text, View } from 'react-native';

import { errorMessage } from '@/api/client';
import { queryKeys } from '@/api/queries';
import { fleetApi } from '@/api/scootly';
import { AppText } from '@/components/AppText';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import { validateNewVehicle, type NewVehicleInput } from '@/domain/fleet';
import { haptics } from '@/lib/haptics';
import { ensureLocationPermission, getCurrentLocation } from '@/lib/location';
import { makeStyles, radius, space, textVariants } from '@/theme';

/** Filodaki bilinen modeller: tek dokunuşla marka ve menzil. */
const MODELS = [
  { brand: 'Segway Ninebot', rangeKm: 40 },
  { brand: 'Xiaomi Pro 2', rangeKm: 30 },
];

const coordinate = (value: number | undefined) => (value === undefined ? '' : value.toFixed(6));

/** Yeni araç kaydı (yalnızca filo yöneticisi). Kayıttan sonra aracın sayfası QR koduyla açılır. */
export default function NewVehicleScreen() {
  const styles = useStyles();
  const router = useRouter();
  const queryClient = useQueryClient();
  const params = useLocalSearchParams<{ lat?: string; lng?: string }>();
  const mapCenter =
    params.lat && params.lng ? { latitude: Number(params.lat), longitude: Number(params.lng) } : undefined;

  const [input, setInput] = useState<NewVehicleInput>({
    brand: MODELS[0].brand,
    rangeKm: String(MODELS[0].rangeKm),
    batteryPercentage: '100',
    latitude: coordinate(mapCenter?.latitude),
    longitude: coordinate(mapCenter?.longitude),
  });
  const [errors, setErrors] = useState<Partial<Record<keyof NewVehicleInput, string>>>({});
  const [locating, setLocating] = useState(false);
  const [locationError, setLocationError] = useState<string | null>(null);

  const update = (field: keyof NewVehicleInput, value: string) => {
    setInput((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => ({ ...previous, [field]: undefined }));
  };

  const fillMyLocation = async () => {
    setLocating(true);
    setLocationError(null);
    try {
      const granted = await ensureLocationPermission(true);
      const location = granted ? await getCurrentLocation() : null;
      if (!location) {
        setLocationError('Konum alınamadı. Konum iznini kontrol et ya da koordinatları elle yaz.');
        return;
      }
      setInput((previous) => ({
        ...previous,
        latitude: coordinate(location.latitude),
        longitude: coordinate(location.longitude),
      }));
      setErrors((previous) => ({ ...previous, latitude: undefined, longitude: undefined }));
    } finally {
      setLocating(false);
    }
  };

  const register = useMutation({
    mutationFn: fleetApi.register,
    onSuccess: async ({ id }) => {
      haptics.success();
      await queryClient.invalidateQueries({ queryKey: queryKeys.fleet });
      router.replace({ pathname: '/fleet/vehicle/[id]', params: { id } });
    },
    onError: () => haptics.error(),
  });

  const onSave = () => {
    const result = validateNewVehicle(input);
    if (!result.ok) {
      setErrors(result.errors);
      haptics.warning();
      return;
    }
    register.mutate(result.request);
  };

  return (
    <Screen edges={['bottom']} keyboard>
      <View style={styles.section}>
        <AppText variant="label">Model</AppText>
        <View style={styles.models}>
          {MODELS.map((model) => {
            const selected = input.brand === model.brand;
            return (
              <Pressable
                key={model.brand}
                accessibilityRole="radio"
                accessibilityState={{ selected }}
                onPress={() => {
                  update('brand', model.brand);
                  update('rangeKm', String(model.rangeKm));
                }}
                style={({ pressed }) => [styles.model, selected && styles.modelSelected, pressed && styles.pressed]}>
                <Text style={[styles.modelName, selected && styles.modelNameSelected]}>{model.brand}</Text>
                <Text style={[styles.modelRange, selected && styles.modelNameSelected]}>{`${model.rangeKm} km`}</Text>
              </Pressable>
            );
          })}
        </View>
      </View>

      <TextField
        label="Marka"
        value={input.brand}
        onChangeText={(value) => update('brand', value)}
        error={errors.brand}
        autoCapitalize="words"
        maxLength={100}
      />
      <View style={styles.row}>
        <TextField
          label="Menzil (km)"
          value={input.rangeKm}
          onChangeText={(value) => update('rangeKm', value)}
          error={errors.rangeKm}
          keyboardType="number-pad"
          maxLength={4}
          containerStyle={styles.half}
        />
        <TextField
          label="Batarya (%)"
          value={input.batteryPercentage}
          onChangeText={(value) => update('batteryPercentage', value)}
          error={errors.batteryPercentage}
          keyboardType="number-pad"
          maxLength={3}
          containerStyle={styles.half}
        />
      </View>

      <View style={styles.section}>
        <AppText variant="label">Konum</AppText>
        <AppText variant="caption" muted>
          {mapCenter ? 'Haritanın ortası seçili. Aracın yanındaysan kendi konumunu kullan.' : 'Aracın durduğu yer.'}
        </AppText>
        <View style={styles.row}>
          <TextField
            label="Enlem"
            value={input.latitude}
            onChangeText={(value) => update('latitude', value)}
            error={errors.latitude}
            keyboardType="numbers-and-punctuation"
            containerStyle={styles.half}
          />
          <TextField
            label="Boylam"
            value={input.longitude}
            onChangeText={(value) => update('longitude', value)}
            error={errors.longitude}
            keyboardType="numbers-and-punctuation"
            containerStyle={styles.half}
          />
        </View>
        <Button
          title="Konumumu kullan"
          variant="secondary"
          icon="locate"
          loading={locating}
          onPress={() => void fillMyLocation()}
        />
        {locationError ? <Notice tone="warning">{locationError}</Notice> : null}
      </View>

      {register.isError ? <Notice tone="danger">{errorMessage(register.error)}</Notice> : null}

      <Button title="Aracı kaydet" size="lg" icon="checkmark" loading={register.isPending} onPress={onSave} />
      <AppText variant="caption" muted align="center">
        Kayıttan sonra aracın QR kodu açılır; yazdırıp scooter&apos;a yapıştırabilirsin.
      </AppText>
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  section: {
    gap: space.sm,
  },
  models: {
    flexDirection: 'row',
    gap: space.sm,
  },
  model: {
    flex: 1,
    gap: 2,
    padding: space.md,
    borderRadius: radius.control,
    borderWidth: 1.5,
    borderColor: t.lineStrong,
    backgroundColor: t.surface,
  },
  modelSelected: {
    borderColor: t.strong,
    backgroundColor: t.strong,
  },
  pressed: {
    opacity: 0.8,
  },
  modelName: {
    ...textVariants.bodyStrong,
    color: t.text,
  },
  modelRange: {
    ...textVariants.caption,
    color: t.textMuted,
  },
  modelNameSelected: {
    color: t.onStrong,
  },
  row: {
    flexDirection: 'row',
    gap: space.md,
  },
  half: {
    flex: 1,
  },
}));
