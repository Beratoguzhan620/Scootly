import { View } from 'react-native';

import { useFleetAction } from './useFleetAction';
import { errorMessage } from '@/api/client';
import type { Vehicle } from '@/api/types';
import { Button, type ButtonVariant } from '@/components/Button';
import type { IconName } from '@/components/Icon';
import { Notice } from '@/components/Notice';
import { availableActions, fleetActionMeta, IN_RIDE_NOTE, type FleetAction } from '@/domain/fleet';
import { makeStyles, space } from '@/theme';

const look: Record<FleetAction, { variant: ButtonVariant; icon: IconName }> = {
  service: { variant: 'primary', icon: 'checkmark-circle-outline' },
  maintenance: { variant: 'secondary', icon: 'construct-outline' },
  lost: { variant: 'dangerGhost', icon: 'alert-circle-outline' },
};

/** Aracın durumuna göre yapılabilecek işlemler. Sürüşteki araçta işlem yoktur. */
export function FleetActions({ vehicle }: { vehicle: Vehicle }) {
  const styles = useStyles();
  const action = useFleetAction();
  const actions = availableActions(vehicle.status);
  const pending = action.isPending ? action.variables?.action : null;
  const done = action.isSuccess && action.variables?.vehicle.id === vehicle.id ? action.variables.action : null;

  if (actions.length === 0) return <Notice tone="info">{IN_RIDE_NOTE}</Notice>;

  return (
    <View style={styles.actions}>
      {done ? <Notice tone="success">{fleetActionMeta[done].done}</Notice> : null}
      {action.isError ? <Notice tone="danger">{errorMessage(action.error)}</Notice> : null}
      {actions.map((item) => (
        <Button
          key={item}
          title={fleetActionMeta[item].label}
          variant={look[item].variant}
          icon={look[item].icon}
          loading={pending === item}
          disabled={action.isPending}
          onPress={() => void action.perform(vehicle, item)}
        />
      ))}
    </View>
  );
}

const useStyles = makeStyles(() => ({
  actions: {
    gap: space.sm,
  },
}));
