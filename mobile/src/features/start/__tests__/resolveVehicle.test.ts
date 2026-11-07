import { QueryClient } from '@tanstack/react-query';

import { ApiError } from '@/api/client';
import { queryKeys } from '@/api/queries';
import { vehiclesApi } from '@/api/scootly';
import type { Vehicle } from '@/api/types';

import { resolveVehicle } from '../resolveVehicle';

jest.mock('@/api/scootly', () => ({
  vehiclesApi: { get: jest.fn(), list: jest.fn() },
}));

const get = vehiclesApi.get as jest.Mock;
const list = vehiclesApi.list as jest.Mock;

const vehicle = (id: string, status: Vehicle['status'] = 'Available'): Vehicle => ({
  id,
  latitude: 41,
  longitude: 29,
  batteryPercentage: 80,
  status,
  brand: 'Segway',
  rangeKm: 40,
});

const RESERVED = '00000012-5555-4000-8000-000000000018';
const CACHED = '3f2b8c1a-1111-4000-8000-000000000001';
const NEARBY = '9a7e4d2b-2222-4000-8000-000000000002';

beforeEach(() => {
  get.mockReset().mockImplementation((id: string) => Promise.resolve(vehicle(id)));
  list.mockReset().mockResolvedValue({ items: [vehicle(NEARBY)], pageNumber: 1, pageSize: 100, totalCount: 1, totalPages: 1 });
});

function context(reservedVehicleId: string | null = null, location: { latitude: number; longitude: number } | null = null) {
  // Çöp toplama zamanlayıcısı kurulmasın (Jest açık kalmasın).
  const queryClient = new QueryClient({ defaultOptions: { queries: { gcTime: Infinity } } });
  queryClient.setQueryData(queryKeys.vehiclesInBounds(null), { items: [vehicle(CACHED)] });
  return { queryClient, reservedVehicleId, location };
}

describe('resolveVehicle', () => {
  it('QR kodundaki kimlikle aracın güncel durumunu getirir', async () => {
    await expect(resolveVehicle({ kind: 'id', id: NEARBY }, context())).resolves.toMatchObject({ id: NEARBY });
    expect(get).toHaveBeenCalledWith(NEARBY, undefined);
  });

  it('plaka kodunu önce rezervasyonda, sonra haritada yüklü araçlarda arar', async () => {
    await resolveVehicle({ kind: 'code', code: '00000012' }, context(RESERVED));
    expect(get).toHaveBeenLastCalledWith(RESERVED, undefined);
    await resolveVehicle({ kind: 'code', code: '3f2b8c1a' }, context(RESERVED));
    expect(get).toHaveBeenLastCalledWith(CACHED, undefined);
    expect(list).not.toHaveBeenCalled();
  });

  it('bulamazsa sürücünün çevresindeki müsait araçlara bakar', async () => {
    await resolveVehicle({ kind: 'code', code: '9a7e4d2b' }, context(null, { latitude: 41, longitude: 29 }));
    expect(list).toHaveBeenCalledWith(expect.objectContaining({ onlyAvailable: true, pageSize: 100 }), undefined);
    expect(get).toHaveBeenLastCalledWith(NEARBY, undefined);
  });

  it('kod hiçbir araca uymuyorsa ne yapılacağını söyler', async () => {
    await expect(resolveVehicle({ kind: 'code', code: 'abcdef12' }, context(null, null))).rejects.toThrow(
      'Bu kodla yakında müsait bir scooter bulunamadı',
    );
  });

  it('müsait olmayan aracın 404’ünü anlaşılır bir mesaja çevirir', async () => {
    get.mockRejectedValueOnce(new ApiError({ kind: 'http', status: 404, message: 'Kayıt bulunamadı.' }));
    await expect(resolveVehicle({ kind: 'id', id: NEARBY }, context())).rejects.toThrow('şu anda kiralanamıyor');
  });
});
