import { vehicleQrPayload } from '@/domain/vehicleCode';

import { qrMatrix } from '../qr';

describe('qrMatrix', () => {
  it('araç bağlantısını QR koduna çevirir; köşelerde konum desenleri bulunur', () => {
    const { size, path } = qrMatrix(vehicleQrPayload('3f2b8c1a-1111-4000-8000-000000000001'));
    // 54 baytlık bağlantı, M düzeyinde sürüm 4'e (33×33) sığar.
    expect(size).toBe(33);
    // Sol üst ve sağ üst konum desenlerinin üst kenarı: 7 modüllük koyu çizgiler.
    expect(path.startsWith('M0 0h7v1h-7z')).toBe(true);
    expect(path).toContain(`M${size - 7} 0h7v1h-7z`);
    // Sol alt konum deseninin alt kenarı
    expect(path).toContain(`M0 ${size - 1}h7v1h-7z`);
  });

  it('aynı içerik için aynı yolu üretir', () => {
    expect(qrMatrix('scootly').path).toBe(qrMatrix('scootly').path);
  });
});
