import createQrCode from 'qrcode-generator';

export type QrMatrix = {
  /** Kenar başına modül sayısı */
  size: number;
  /** Koyu modüller, satır satır birleştirilmiş SVG yolu (1 modül = 1 birim) */
  path: string;
};

/**
 * QR kodunu SVG yolu olarak üretir. Yan yana koyu modüller tek dikdörtgende birleşir; böylece yol kısa kalır.
 * Hata düzeltme M: çıkartma biraz yıpransa da okunur.
 */
export function qrMatrix(value: string): QrMatrix {
  const qr = createQrCode(0, 'M');
  qr.addData(value);
  qr.make();
  const size = qr.getModuleCount();
  let path = '';
  for (let row = 0; row < size; row += 1) {
    let column = 0;
    while (column < size) {
      if (!qr.isDark(row, column)) {
        column += 1;
        continue;
      }
      const start = column;
      while (column < size && qr.isDark(row, column)) column += 1;
      path += `M${start} ${row}h${column - start}v1h${start - column}z`;
    }
  }
  return { size, path };
}
