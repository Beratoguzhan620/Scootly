import { matchVehicleCode, normalizeCodeInput, parseVehicleCode, vehicleQrPayload } from '../vehicleCode';

const ID = '3f2b8c1a-1111-4000-8000-000000000001';

describe('QR ve plaka kodu', () => {
  it('uygulamanın QR bağlantısından kimliği okur', () => {
    expect(parseVehicleCode(vehicleQrPayload(ID))).toEqual({ kind: 'id', id: ID });
    expect(vehicleQrPayload(ID.toUpperCase())).toBe(`scootly://vehicle/${ID}`);
  });

  it('kimlik geçen başka bağlantıları ve çıplak kimliği de kabul eder', () => {
    expect(parseVehicleCode(`https://scootly.example/v/${ID.toUpperCase()}?ref=qr`)).toEqual({ kind: 'id', id: ID });
    expect(parseVehicleCode(`  ${ID}\n`)).toEqual({ kind: 'id', id: ID });
  });

  it('plakadaki 8 haneli kodu kabul eder', () => {
    expect(parseVehicleCode('3F2B8C1A')).toEqual({ kind: 'code', code: '3f2b8c1a' });
    expect(parseVehicleCode('3f2b 8c1a')).toEqual({ kind: 'code', code: '3f2b8c1a' });
  });

  it('başka QR kodlarını reddeder', () => {
    expect(parseVehicleCode('https://example.com')).toBeNull();
    expect(parseVehicleCode('WIFI:S:ev;P:1234;;')).toBeNull();
    expect(parseVehicleCode('3F2B8C1')).toBeNull();
    expect(parseVehicleCode('ZZZZZZZZ')).toBeNull();
  });

  it('kodu araçla eşleştirir', () => {
    const vehicles = [{ id: '9a7e4d2b-2222-4000-8000-000000000002' }, { id: ID }];
    expect(matchVehicleCode(vehicles, '3F2B8C1A')).toEqual({ id: ID });
    expect(matchVehicleCode(vehicles, 'abcdef12')).toBeNull();
  });

  it('elle girişi plaka gibi yazar', () => {
    expect(normalizeCodeInput('3f2b-8c1a9')).toBe('3F2B8C1A');
    expect(normalizeCodeInput('xyz3f')).toBe('3F');
  });
});
