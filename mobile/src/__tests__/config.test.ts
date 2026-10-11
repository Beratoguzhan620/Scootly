import { accountFromToken, isDriver } from '../auth/account';
import { resolveApiBaseUrl } from '../config';

describe('resolveApiBaseUrl', () => {
  it('EXPO_PUBLIC_API_URL her zaman önceliklidir', () => {
    expect(resolveApiBaseUrl('https://api.scootly.dev/', '192.168.1.20:8081')).toBe('https://api.scootly.dev');
  });

  it('geliştirmede Metro’nun çalıştığı bilgisayarın adresini kullanır', () => {
    expect(resolveApiBaseUrl(undefined, '192.168.1.20:8081')).toBe('http://192.168.1.20:5016');
    expect(resolveApiBaseUrl('   ', '192.168.1.20:8081')).toBe('http://192.168.1.20:5016');
  });

  it('hiçbiri yoksa localhost', () => {
    expect(resolveApiBaseUrl(undefined, undefined)).toBe('http://localhost:5016');
  });
});

describe('accountFromToken', () => {
  const base64Url = (value: string) =>
    Buffer.from(value, 'utf8').toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  const token = (payload: Record<string, unknown>) => `e30.${base64Url(JSON.stringify(payload))}.imza`;

  it('uzun ve kısa rol talep adlarını okur', () => {
    const long = accountFromToken(
      token({
        sub: 'u1',
        email: 'emre@example.com',
        'http://schemas.microsoft.com/ws/2008/06/identity/claims/role': ['Driver', 'FieldOperator'],
      }),
    );
    expect(long).toEqual({ id: 'u1', email: 'emre@example.com', roles: ['Driver', 'FieldOperator'] });
    expect(long && isDriver(long)).toBe(true);

    const short = accountFromToken(token({ sub: 'u2', role: 'FleetManager' }));
    expect(short).toEqual({ id: 'u2', email: '', roles: ['FleetManager'] });
    expect(short && isDriver(short)).toBe(false);
  });

  it('sub yoksa null', () => {
    expect(accountFromToken(token({ email: 'x@y.z' }))).toBeNull();
  });
});
