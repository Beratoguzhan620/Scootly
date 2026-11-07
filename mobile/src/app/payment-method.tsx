import { useRouter } from 'expo-router';
import { useRef, useState } from 'react';
import { View, type TextInput } from 'react-native';

import { errorMessage } from '@/api/client';
import { Button } from '@/components/Button';
import { Notice } from '@/components/Notice';
import { PaymentCard } from '@/components/PaymentCard';
import { Screen } from '@/components/Screen';
import { TextField } from '@/components/TextField';
import {
  cvcLength,
  detectBrand,
  digitsOnly,
  formatCardExpiry,
  formatCardNumber,
  formatExpiry,
  testCardInput,
  validateCard,
  type CardField,
  type CardInput,
} from '@/domain/card';
import { confirm } from '@/lib/confirm';
import { haptics } from '@/lib/haptics';
import { usePayment } from '@/payments/PaymentProvider';
import { makeStyles, space } from '@/theme';

const EMPTY: CardInput = { number: '', expiry: '', cvc: '', holder: '' };

/** Ödeme kartı (test modu): kayıtlı kart ya da yeni kart formu. */
export default function PaymentMethodScreen() {
  const styles = useStyles();
  const router = useRouter();
  const { card, saveCard, removeCard } = usePayment();
  const [editing, setEditing] = useState(card === null);
  const [input, setInput] = useState<CardInput>(EMPTY);
  const [errors, setErrors] = useState<Partial<Record<CardField, string>>>({});
  const [saving, setSaving] = useState(false);
  const [saveError, setSaveError] = useState<string | null>(null);
  const expiryRef = useRef<TextInput>(null);
  const cvcRef = useRef<TextInput>(null);
  const holderRef = useRef<TextInput>(null);

  const brand = detectBrand(input.number);
  const digits = digitsOnly(input.number);

  const update = (field: CardField, value: string) => {
    setInput((previous) => ({ ...previous, [field]: value }));
    setErrors((previous) => ({ ...previous, [field]: undefined }));
  };

  const onSave = async () => {
    const result = validateCard(input);
    if (!result.ok) {
      setErrors(result.errors);
      haptics.warning();
      return;
    }
    setSaving(true);
    setSaveError(null);
    try {
      await saveCard(result.card);
      // Tam numara ve güvenlik kodu ekrandan da silinsin.
      setInput(EMPTY);
      haptics.success();
      if (router.canGoBack()) router.back();
      else setEditing(false);
    } catch (error) {
      setSaveError(errorMessage(error, 'Kart kaydedilemedi. Tekrar dene.'));
    } finally {
      setSaving(false);
    }
  };

  const onRemove = async () => {
    const confirmed = await confirm({
      title: 'Kartı kaldır',
      message: 'Yeni bir sürüşe başlamak için yeniden kart eklemen gerekir.',
      confirmLabel: 'Kaldır',
      destructive: true,
    });
    if (!confirmed) return;
    await removeCard();
    setEditing(true);
  };

  if (card && !editing) {
    return (
      <Screen edges={['bottom']}>
        <PaymentCard brand={card.brand} last4={card.last4} holder={card.holder} expiry={formatCardExpiry(card)} />
        <Notice tone="info" title="Test modu">
          Sürüş ücretleri bu karttan çekilmiş gibi gösterilir; gerçek ödeme alınmaz.
        </Notice>
        <View style={styles.actions}>
          <Button title="Başka kart ekle" variant="secondary" icon="card-outline" onPress={() => setEditing(true)} />
          <Button title="Kartı kaldır" variant="dangerGhost" onPress={() => void onRemove()} />
        </View>
      </Screen>
    );
  }

  return (
    <Screen edges={['bottom']} keyboard>
      <PaymentCard
        brand={brand}
        last4={digits.length > 4 ? digits.slice(-4) : ''}
        holder={input.holder.trim()}
        expiry={input.expiry}
      />

      <Notice tone="info" title="Test modu">
        Gerçek ödeme alınmaz. Kart numaran ve güvenlik kodun saklanmaz; bu telefonda yalnızca kartın markası, son 4 hanesi
        ve son kullanma tarihi tutulur.
      </Notice>

      <View style={styles.form}>
        <TextField
          label="Kart numarası"
          value={input.number}
          onChangeText={(value) => update('number', formatCardNumber(value))}
          error={errors.number}
          placeholder="0000 0000 0000 0000"
          keyboardType="number-pad"
          autoComplete="cc-number"
          textContentType="creditCardNumber"
          maxLength={23}
          returnKeyType="next"
          onSubmitEditing={() => expiryRef.current?.focus()}
        />
        <View style={styles.row}>
          <TextField
            ref={expiryRef}
            label="Son kullanma"
            value={input.expiry}
            onChangeText={(value) => update('expiry', formatExpiry(value))}
            error={errors.expiry}
            placeholder="AA/YY"
            keyboardType="number-pad"
            autoComplete="cc-exp"
            maxLength={5}
            returnKeyType="next"
            onSubmitEditing={() => cvcRef.current?.focus()}
            containerStyle={styles.half}
          />
          <TextField
            ref={cvcRef}
            label="Güvenlik kodu"
            value={input.cvc}
            onChangeText={(value) => update('cvc', digitsOnly(value))}
            error={errors.cvc}
            placeholder={'0'.repeat(cvcLength(brand))}
            keyboardType="number-pad"
            autoComplete="cc-csc"
            secureTextEntry
            maxLength={cvcLength(brand)}
            returnKeyType="next"
            onSubmitEditing={() => holderRef.current?.focus()}
            containerStyle={styles.half}
          />
        </View>
        <TextField
          ref={holderRef}
          label="Kart üzerindeki ad"
          value={input.holder}
          onChangeText={(value) => update('holder', value)}
          error={errors.holder}
          placeholder="Ad Soyad"
          autoCapitalize="words"
          autoComplete="cc-name"
          textContentType="name"
          returnKeyType="done"
          onSubmitEditing={() => void onSave()}
        />
      </View>

      {saveError ? <Notice tone="danger">{saveError}</Notice> : null}

      <Button title="Kartı kaydet" size="lg" icon="checkmark" loading={saving} onPress={() => void onSave()} />
      <Button
        title="Test kartını doldur"
        variant="ghost"
        icon="flask-outline"
        onPress={() => {
          setInput(testCardInput());
          setErrors({});
        }}
      />
      {card ? <Button title="Vazgeç" variant="ghost" onPress={() => setEditing(false)} /> : null}
    </Screen>
  );
}

const useStyles = makeStyles(() => ({
  form: {
    gap: space.lg,
  },
  row: {
    flexDirection: 'row',
    gap: space.md,
  },
  half: {
    flex: 1,
  },
  actions: {
    gap: space.sm,
  },
}));
