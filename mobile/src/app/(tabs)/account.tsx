import { useRouter } from 'expo-router';
import { StyleSheet, View } from 'react-native';

import { useApiBaseUrl } from '@/api/server';
import { useAccount, useAuth } from '@/auth/AuthProvider';
import { AppText, Code } from '@/components/AppText';
import { Badge } from '@/components/Badge';
import { Button } from '@/components/Button';
import { Card } from '@/components/Card';
import { KeyValueRow, ListGroup } from '@/components/ListGroup';
import { PanelFooter } from '@/components/PanelFooter';
import { Screen } from '@/components/Screen';
import { appConfig } from '@/config';
import { roleLabel } from '@/domain/labels';
import { confirm } from '@/lib/confirm';
import { shortId } from '@/lib/format';
import { spacing } from '@/theme';

export default function AccountScreen() {
  const router = useRouter();
  const account = useAccount();
  const apiBaseUrl = useApiBaseUrl();
  const { signOut } = useAuth();

  const onSignOut = async () => {
    const confirmed = await confirm({
      title: 'Çıkış yap',
      message: 'Oturumun bu cihazda kapatılacak.',
      confirmLabel: 'Çıkış Yap',
    });
    if (confirmed) await signOut();
  };

  return (
    <Screen>
      <Card header="Hesap Bilgileri" bodyStyle={styles.cardBody}>
        <ListGroup flush>
          <KeyValueRow label="E-posta" value={account.email} />
          <KeyValueRow
            label="Rol"
            value={
              <View style={styles.badges}>
                {account.roles.map((role) => (
                  <Badge key={role} label={roleLabel(role)} tone="primary" />
                ))}
              </View>
            }
          />
          <KeyValueRow label="Kullanıcı kimliği" value={<Code>{shortId(account.id)}</Code>} />
        </ListGroup>
      </Card>

      <View style={styles.actions}>
        <Button title="Parolayı Değiştir" variant="outline-primary" onPress={() => router.push('/change-password')} />
        <Button title="Çıkış Yap" variant="outline-secondary" onPress={onSignOut} />
      </View>

      <Card header="Hesabı Sil" bodyStyle={styles.dangerBody}>
        <AppText muted>
          Hesabın kalıcı olarak silinir ve sürüş konumların anonimleştirilir. Bu işlem geri alınamaz.
        </AppText>
        <Button title="Hesabımı Sil" variant="outline-danger" onPress={() => router.push('/delete-account')} />
      </Card>

      <PanelFooter>
        <AppText variant="small" muted>
          Sürüm {appConfig.version}
        </AppText>
        {appConfig.serverEditable ? (
          <AppText variant="small" muted>
            Sunucu <Code>{apiBaseUrl}</Code>
          </AppText>
        ) : null}
      </PanelFooter>
    </Screen>
  );
}

const styles = StyleSheet.create({
  cardBody: {
    paddingVertical: spacing[1],
  },
  badges: {
    flexDirection: 'row',
    flexWrap: 'wrap',
    justifyContent: 'flex-end',
    gap: spacing[1],
  },
  actions: {
    gap: spacing[2],
  },
  dangerBody: {
    gap: spacing[3],
  },
});
