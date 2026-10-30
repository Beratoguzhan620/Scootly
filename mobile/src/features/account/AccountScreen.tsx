import { useRouter } from 'expo-router';
import { Text, View } from 'react-native';

import { useApiBaseUrl } from '@/api/server';
import { useAccount, useAppMode, useAuth } from '@/auth/AuthProvider';
import { AppText } from '@/components/AppText';
import { ListRow, RowGroup } from '@/components/Rows';
import { Screen } from '@/components/Screen';
import { StatusPill } from '@/components/StatusPill';
import { Surface } from '@/components/Surface';
import { appConfig } from '@/config';
import { cardLabel } from '@/domain/card';
import { roleLabel } from '@/domain/labels';
import { confirm } from '@/lib/confirm';
import { haptics } from '@/lib/haptics';
import { usePayment } from '@/payments/PaymentProvider';
import { INK, makeStyles, space, textVariants } from '@/theme';

/** E-postanın ilk harfi: profil diskinde. */
function initialOf(email: string): string {
  return (email.trim()[0] ?? '?').toLocaleUpperCase('tr-TR');
}

/** Hesap sekmesi; sürücü ve filo modunda aynı ekran. */
export function AccountScreen() {
  const styles = useStyles();
  const router = useRouter();
  const account = useAccount();
  const apiBaseUrl = useApiBaseUrl();
  const { signOut } = useAuth();
  const { mode, modes, setMode } = useAppMode();
  const { card } = usePayment();
  const otherMode = modes.find((item) => item !== mode);

  const switchMode = async () => {
    if (!otherMode) return;
    haptics.select();
    await setMode(otherMode);
  };

  const onSignOut = async () => {
    const confirmed = await confirm({
      title: 'Çıkış yap',
      message: 'Oturumun bu cihazda kapatılacak.',
      confirmLabel: 'Çıkış yap',
    });
    if (confirmed) await signOut();
  };

  return (
    <Screen title="Hesap" edges={['top']}>
      <Surface style={styles.profile}>
        <View style={styles.avatar} accessibilityElementsHidden importantForAccessibility="no">
          <Text style={styles.avatarText}>{initialOf(account.email)}</Text>
        </View>
        <View style={styles.profileText}>
          <AppText variant="heading" numberOfLines={1}>
            {account.email}
          </AppText>
          <View style={styles.roles}>
            {account.roles.map((role) => (
              <StatusPill key={role} label={roleLabel(role)} tone="primary" />
            ))}
          </View>
        </View>
      </Surface>

      {otherMode ? (
        <RowGroup>
          <ListRow
            icon={otherMode === 'fleet' ? 'construct-outline' : 'navigate-outline'}
            title={otherMode === 'fleet' ? 'Filo yönetimine geç' : 'Sürücü moduna geç'}
            subtitle={
              otherMode === 'fleet'
                ? 'Tüm araçları gör; bakıma al, kayıp işaretle, yeni araç ekle.'
                : 'Scooter kirala ve sürüşlerini gör.'
            }
            onPress={() => void switchMode()}
          />
        </RowGroup>
      ) : null}

      <RowGroup>
        {mode === 'driver' ? (
          <ListRow
            icon="card-outline"
            title="Ödeme yöntemi"
            subtitle={card ? `${cardLabel(card)} · test modu` : 'Kart eklenmedi'}
            onPress={() => router.push('/payment-method')}
          />
        ) : null}
        <ListRow icon="key-outline" title="Parolayı değiştir" onPress={() => router.push('/change-password')} />
        <ListRow icon="log-out-outline" title="Çıkış yap" onPress={() => void onSignOut()} />
      </RowGroup>

      <RowGroup>
        <ListRow
          icon="trash-outline"
          title="Hesabımı sil"
          subtitle="Hesap kalıcı olarak silinir; sürüş konumların anonimleştirilir."
          destructive
          onPress={() => router.push('/delete-account')}
        />
      </RowGroup>

      <View style={styles.footer}>
        <AppText variant="caption" muted>
          {`Scootly ${appConfig.version}`}
        </AppText>
        {appConfig.serverEditable ? (
          <AppText variant="caption" muted numberOfLines={1}>
            {`Sunucu: ${apiBaseUrl}`}
          </AppText>
        ) : null}
      </View>
    </Screen>
  );
}

const useStyles = makeStyles((t) => ({
  profile: {
    flexDirection: 'row',
    alignItems: 'center',
    gap: space.lg,
  },
  avatar: {
    width: 56,
    height: 56,
    borderRadius: 28,
    alignItems: 'center',
    justifyContent: 'center',
    backgroundColor: t.brand,
  },
  avatarText: {
    ...textVariants.numeral,
    fontSize: 28,
    lineHeight: 32,
    color: INK,
  },
  profileText: {
    flex: 1,
    gap: space.xs + 2,
  },
  roles: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    gap: space.xs + 2,
  },
  footer: {
    marginTop: 'auto',
    paddingTop: space.lg,
    gap: 2,
    alignItems: 'center',
  },
}));
