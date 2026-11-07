import { isLoopbackUrl, normalizeApiUrl } from '../url';

describe('normalizeApiUrl', () => {
  it.each([
    ['192.168.1.20:5017', 'http://192.168.1.20:5017'],
    ['  http://192.168.1.20:5017/  ', 'http://192.168.1.20:5017'],
    ['HTTPS://api.scootly.dev//', 'https://api.scootly.dev'],
    ['http://10.0.2.2:5016', 'http://10.0.2.2:5016'],
    ['http://localhost', 'http://localhost'],
    ['https://ornek.com/scootly', 'https://ornek.com/scootly'],
    ['http://[::1]:5016', 'http://[::1]:5016'],
  ])('%s → %s', (input, expected) => {
    expect(normalizeApiUrl(input)).toBe(expected);
  });

  it.each([
    [''],
    ['   '],
    ['ftp://192.168.1.20'],
    ['http://'],
    ['http://192.168.1.20:99999'],
    ['http://192.168.1.20:0'],
    ['http://bir adres'],
    ['http://..'],
    ['http://ornek.com?x=1'],
  ])('%j geçersiz', (input) => {
    expect(normalizeApiUrl(input)).toBeNull();
  });
});

describe('isLoopbackUrl', () => {
  it('cihazın kendisini gösteren adresleri tanır', () => {
    expect(isLoopbackUrl('http://localhost:5016')).toBe(true);
    expect(isLoopbackUrl('http://127.0.0.1:5016/')).toBe(true);
    expect(isLoopbackUrl('http://[::1]:5016')).toBe(true);
  });

  it('yerel ağ ve emülatör adreslerini tanımaz', () => {
    expect(isLoopbackUrl('http://192.168.1.20:5017')).toBe(false);
    expect(isLoopbackUrl('http://10.0.2.2:5016')).toBe(false);
    expect(isLoopbackUrl('http://localhost.ornek.com')).toBe(false);
  });
});
