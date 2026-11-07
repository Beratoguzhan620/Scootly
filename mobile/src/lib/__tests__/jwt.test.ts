import { decodeJwtPayload, getJwtExpiry, isJwtExpired } from '../jwt';

const base64Url = (value: string) =>
  Buffer.from(value, 'utf8').toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');

const makeToken = (payload: Record<string, unknown>) =>
  `${base64Url(JSON.stringify({ alg: 'HS256', typ: 'JWT' }))}.${base64Url(JSON.stringify(payload))}.imza`;

describe('JWT gövdesi', () => {
  const exp = Math.floor(Date.UTC(2026, 9, 11, 9, 0, 0) / 1000);
  const token = makeToken({ sub: 'kullanici-1', email: 'çağrı@örnek.com', exp });

  it('UTF-8 karakterleri doğru çözer', () => {
    expect(decodeJwtPayload(token)).toMatchObject({ sub: 'kullanici-1', email: 'çağrı@örnek.com' });
  });

  it('bitiş zamanını milisaniye olarak verir', () => {
    expect(getJwtExpiry(token)).toBe(exp * 1000);
  });

  it('saat farkı payıyla süre dolumunu hesaplar', () => {
    expect(isJwtExpired(token, exp * 1000 - 60_000)).toBe(false);
    expect(isJwtExpired(token, exp * 1000 - 10_000)).toBe(true); // 30 sn pay içinde
    expect(isJwtExpired(token, exp * 1000 - 10_000, 0)).toBe(false);
  });

  it('bozuk token için null döner ve onu süresi dolmuş saymaz', () => {
    expect(decodeJwtPayload('bozuk')).toBeNull();
    expect(decodeJwtPayload('a.!!!.c')).toBeNull();
    expect(getJwtExpiry(makeToken({ sub: 'x' }))).toBeNull();
    expect(isJwtExpired('bozuk')).toBe(false);
  });
});
