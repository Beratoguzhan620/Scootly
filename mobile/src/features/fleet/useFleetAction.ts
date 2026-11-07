import { useMutation, useQueryClient } from '@tanstack/react-query';

import { queryKeys } from '@/api/queries';
import { fleetApi } from '@/api/scootly';
import type { Vehicle } from '@/api/types';
import { fleetActionMeta, type FleetAction } from '@/domain/fleet';
import { confirm } from '@/lib/confirm';
import { haptics } from '@/lib/haptics';

const run: Record<FleetAction, (id: string) => Promise<void>> = {
  maintenance: fleetApi.sendToMaintenance,
  lost: fleetApi.markLost,
  service: fleetApi.returnToService,
};

/** Araç durumu değiştirme (bakım, kayıp, hizmete dönüş): onay sorar, bitince filo verilerini yeniler. */
export function useFleetAction() {
  const queryClient = useQueryClient();
  const mutation = useMutation({
    mutationFn: ({ vehicle, action }: { vehicle: Vehicle; action: FleetAction }) => run[action](vehicle.id),
    onSuccess: async () => {
      haptics.success();
      await queryClient.invalidateQueries({ queryKey: queryKeys.fleet });
    },
    onError: () => haptics.error(),
  });

  const perform = async (vehicle: Vehicle, action: FleetAction) => {
    const meta = fleetActionMeta[action];
    const confirmed = await confirm({
      title: meta.label,
      message: meta.confirm(vehicle.status),
      confirmLabel: meta.label,
      destructive: meta.destructive,
    });
    if (confirmed) mutation.mutate({ vehicle, action });
  };

  return { ...mutation, perform };
}
