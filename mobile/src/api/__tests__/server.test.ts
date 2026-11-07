import { checkServer } from '../server';

const respond = (status: number) => jest.fn(async () => ({ ok: status >= 200 && status < 300, status }) as Response);

describe('checkServer', () => {
  it('sağlık ucunu çağırır ve hazırsa başarılı döner', async () => {
    const fetchMock = respond(200);
    const result = await checkServer('http://192.168.1.20:5017', 1_000, fetchMock as unknown as typeof fetch);
    expect(fetchMock).toHaveBeenCalledWith('http://192.168.1.20:5017/health/ready', expect.anything());
    expect(result).toMatchObject({ ok: true, tone: 'success' });
  });

  it('503: API ayakta ama bağımlılıkları hazır değil', async () => {
    const result = await checkServer('http://api', 1_000, respond(503) as unknown as typeof fetch);
    expect(result).toMatchObject({ ok: false, tone: 'warning' });
    expect(result.message).toMatch(/hazır değil/);
  });

  it('404: yanlış port (ör. web paneli)', async () => {
    const result = await checkServer('http://api', 1_000, respond(404) as unknown as typeof fetch);
    expect(result).toMatchObject({ ok: false, tone: 'danger' });
    expect(result.message).toMatch(/API'si bulunamadı/);
  });

  it('beklenmeyen durum kodunu söyler', async () => {
    const result = await checkServer('http://api', 1_000, respond(500) as unknown as typeof fetch);
    expect(result.message).toMatch(/HTTP 500/);
  });

  it('ağ hatasında aynı Wi-Fi ağını hatırlatır', async () => {
    const fetchMock = jest.fn(async () => {
      throw new TypeError('Network request failed');
    });
    const result = await checkServer('http://api', 1_000, fetchMock as unknown as typeof fetch);
    expect(result).toMatchObject({ ok: false, tone: 'danger' });
    expect(result.message).toMatch(/aynı Wi-Fi/);
  });

  it('yanıt gelmezse zaman aşımına uğrar', async () => {
    const hanging = jest.fn(
      (_url: string, init?: RequestInit) =>
        new Promise<Response>((_resolve, reject) => {
          init?.signal?.addEventListener('abort', () => reject(new Error('aborted')));
        }),
    );
    const result = await checkServer('http://api', 20, hanging as unknown as typeof fetch);
    expect(result.message).toMatch(/zamanında yanıt vermedi/);
  });
});
