/**
 * Scooter'ın QR kodu ve plaka kodu. QR, aracın kimliğini taşıyan bir bağlantıdır: `scootly://vehicle/<id>`.
 * Telefonun kendi kamerasıyla okutulursa uygulama bu bağlantıyla açılır. Plakada kimliğin ilk 8 karakteri yazar;
 * QR okunamazsa sürücü bu kodu elle girebilir.
 */

const GUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i;
export const VEHICLE_CODE_LENGTH = 8;

export type ScannedVehicle = { kind: 'id'; id: string } | { kind: 'code'; code: string };

/** QR'ın içeriği: kimlik geçen bağlantı ya da çıplak kimlik; elle girişte 8 haneli plaka kodu. */
export function parseVehicleCode(raw: string): ScannedVehicle | null {
  const text = raw.trim();
  const guid = GUID.exec(text);
  if (guid) return { kind: 'id', id: guid[0].toLowerCase() };
  const code = text.replace(/[\s-]/g, '');
  if (new RegExp(`^[0-9a-f]{${VEHICLE_CODE_LENGTH}}$`, 'i').test(code)) return { kind: 'code', code: code.toLowerCase() };
  return null;
}

/** Aracın QR koduna yazılan içerik. */
export function vehicleQrPayload(id: string): string {
  return `scootly://vehicle/${id.toLowerCase()}`;
}

/** Plaka kodu (kimliğin ilk 8 karakteri) bu araçlardan hangisine ait? */
export function matchVehicleCode<T extends { id: string }>(vehicles: T[], code: string): T | null {
  const prefix = code.toLowerCase();
  return vehicles.find((vehicle) => vehicle.id.toLowerCase().startsWith(prefix)) ?? null;
}

/** Elle girişte yazılanı plaka gibi gösterir: büyük harf, yalnızca onaltılık karakterler. */
export function normalizeCodeInput(value: string): string {
  return value
    .replace(/[^0-9a-f]/gi, '')
    .slice(0, VEHICLE_CODE_LENGTH)
    .toUpperCase();
}
