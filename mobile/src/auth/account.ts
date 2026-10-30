import type { Account } from '@/api/types';
import { decodeJwtPayload } from '@/lib/jwt';

export const DRIVER_ROLE = 'Driver';

// .NET'in JwtSecurityTokenHandler'ı rol talebini kısa ("role") ya da uzun URI adıyla yazabilir.
const ROLE_CLAIMS = ['role', 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role'];

/**
 * Çevrimdışı açılışta hesap bilgisini token'dan çıkarır (sub = kullanıcı kimliği, email, roller).
 * Çevrimiçiyken her zaman `GET /api/account` kullanılır.
 */
export function accountFromToken(token: string): Account | null {
  const payload = decodeJwtPayload(token);
  if (!payload || typeof payload.sub !== 'string') return null;

  const roles = ROLE_CLAIMS.flatMap((claim) => {
    const value = payload[claim];
    if (typeof value === 'string') return [value];
    if (Array.isArray(value)) return value.filter((item): item is string => typeof item === 'string');
    return [];
  });

  return {
    id: payload.sub,
    email: typeof payload.email === 'string' ? payload.email : '',
    roles,
  };
}

export function isDriver(account: Account): boolean {
  return account.roles.includes(DRIVER_ROLE);
}

export const FLEET_MANAGER_ROLE = 'FleetManager';
export const FIELD_OPERATOR_ROLE = 'FieldOperator';

/** Filo ekibi: tüm araçları görür, durumlarını değiştirir (backend'deki `FleetOperations` politikası). */
export function isFleetStaff(account: Account): boolean {
  return account.roles.includes(FLEET_MANAGER_ROLE) || account.roles.includes(FIELD_OPERATOR_ROLE);
}

/** Yalnızca filo yöneticisi yeni araç kaydedebilir (`FleetManagerOnly`). */
export function isFleetManager(account: Account): boolean {
  return account.roles.includes(FLEET_MANAGER_ROLE);
}

/** driver: scooter kiralama · fleet: filo yönetimi */
export type AppMode = 'driver' | 'fleet';

/** Hesabın açabileceği modlar, varsayılan önce. Hem sürücü hem filo yetkisi olan hesap sürücü olarak açılır. */
export function availableModes(account: Account): AppMode[] {
  const modes: AppMode[] = [];
  if (isDriver(account)) modes.push('driver');
  if (isFleetStaff(account)) modes.push('fleet');
  return modes;
}

/** Kayıtlı tercih hâlâ geçerliyse o, değilse varsayılan mod; hiçbir mod yoksa null. */
export function resolveMode(account: Account, preferred: string | null): AppMode | null {
  const modes = availableModes(account);
  return modes.find((mode) => mode === preferred) ?? modes[0] ?? null;
}
