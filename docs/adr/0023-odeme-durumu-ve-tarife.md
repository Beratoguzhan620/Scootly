# ADR 0023: Ödeme Durumu, Tarife ve Çift Tahsilat Önleme

## Durum
Kabul edildi (27.09.2026).

## Bağlam
- Ücret, tüketicide `(int)TotalMinutes × 2,5` ile hesaplanıyordu: 1 dakikanın altındaki sürüşler bedava, 1 dk 59 sn
  1 dakika ücretleniyordu. Fiyat kuralı domain dışındaydı.
- Ödeme reddi sürüş durumunu `PaymentPending` yapıyor, "tamamlandı" bilgisi kayboluyordu. Bekleyen ödemeler bir daha
  hiç denenmiyordu.
- Ödeme isteği idempotency anahtarı olmadan gönderiliyor ve yeniden deneniyordu; zaman aşımından sonra yapılan tekrar
  veya mesajın yeniden teslimi çift tahsilata yol açabiliyordu. Kart reddi (402) de yeniden deneniyor ve devre kesiciyi
  açabiliyordu.
- Webhook ucu imzayı yeniden serileştirilmiş JSON üzerinden doğruluyor ve hiçbir şey yapmıyordu.

## Karar
- **Tarife domain'de:** `Tariff` değer nesnesi başlamış her dakikayı ücretlendirir (en az 1 dakika). Ücret, sürüş
  tamamlandığında veya terk edildiğinde `Ride` tarafından hesaplanır ve kaydedilir.
- **Ödeme durumu ayrı:** `PaymentStatus` (None/Pending/Paid/Failed) sürüş durumundan bağımsızdır. Ret bir deneme
  sayılır; 5 denemeden sonra `Failed` olur. Sağlayıcıya ulaşılamaması deneme sayılmaz.
- **Çift tahsilat önleme:** Her deneme `ride-{id}-attempt-{n}` idempotency anahtarıyla gönderilir. Aynı deneme
  tekrarlanırsa (retry, yeniden teslim, kayıt çakışması) sağlayıcı ilk sonucu döndürür ve ikinci kez tahsil etmez.
  Yeni anahtar yalnızca bir ret kaydedildikten sonra üretilir.
- **Dayanıklılık:** Yalnızca geçici hatalar (ağ, zaman aşımı, 5xx, 408, 429) yeniden denenir ve devre kesiciye sayılır.
  Toplam süre ve deneme başına zaman aşımı yapılandırılabilir.
- **Webhook:** `X-Scootly-Signature: t=<unix>,v1=<HMAC-SHA256(secret, "t.hamGövde")>`. İmza ham gövde üzerinden,
  sabit sürede karşılaştırılır; 5 dakikadan eski/ileri zaman damgası reddedilir (tekrar oynatma). Her olay `EventId` ile
  bir kez uygulanır. Webhook onaylar için yetkili ikinci kanaldır: senkron yanıt zaman aşımına uğramış olsa bile ödemeyi
  tamamlar; retler senkron akışta sayıldığı için webhook'ta tekrar sayılmaz.
- **Uzlaştırma:** Worker, bekleyen ödemeleri geri çekilme süresi sonunda yeniden dener ve olayı kaybolmuş sürüşleri
  yakalar (ADR 0015).

## Veri geçişi
`HardenDomainPaymentsAndMessaging` migration'ı eski `PaymentPending` sürüşleri `Completed + Pending` (1 deneme) yapar,
ücreti hiç hesaplanmamış tamamlanmış sürüşlerin ücretini yeni tarifeyle hesaplar ve ödenmiş sürüşleri `Paid` işaretler.
Bu sürüşler Worker ilk çalıştığında uzlaştırma işiyle tahsil edilir.
