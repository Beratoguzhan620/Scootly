# ADR 0016: Sürüş Bitişi Saga'sı, Telafi ve Ödeme Webhook'u

## Durum
Kabul edildi (Hafta 14, 69–70. günler).

## Bağlam
Sürüş bitişi dört adımlık bir iş: sürüşü kapat, ücreti hesapla, ödemeyi al,
aracı serbest bırak. Adımlar farklı zamanlarda, farklı süreçlerde ve (ödeme)
farklı bir sistemde gerçekleşiyor. Tek bir transaction'a sığmıyorlar.

İki fazlı commit (dağıtık transaction) bütün katılımcıların aynı anda kilitlenip
onay vermesini gerektiriyor; ödeme sağlayıcısı buna katılmaz, katılsa bile en
yavaş katılımcı herkesi bekletir.

## Karar

### Koreografi, orkestrasyon değil

```
API: sürüş bitti ──► ride.completed
                          │
Worker: ücret ◄───────────┘ ──► payment.authorization-requested
                                        │
Worker: ödeme sağlayıcısı ◄─────────────┘ ──► payment.authorized
                                                     ▲      │
Ödeme sağlayıcısı ──► webhook (70. gün) ─────────────┘      │
                                                             │
Worker: sürüşü kapat, aracı serbest bırak ◄──────────────────┘
```

Merkezi bir koordinatör yok. Her adım bir öncekinin olayını dinliyor, kendi işini
yapıyor ve bir sonrakinin olayını **outbox'a** yazıyor (ADR 0015). Dört adım,
dört ayrı transaction.

Orkestrasyon (bir "saga yöneticisi" sınıfının adımları sırayla çağırması)
reddedildi: bu projede adım sayısı az ve sıra doğrusal; merkezi koordinatör,
kendi durumunu kalıcı tutması gereken bir bileşen daha demek. Adım sayısı
arttığında ya da dallanma geldiğinde yeniden değerlendirilmeli — koreografinin
bedeli, akışın tamamını tek bir dosyada görememek.

### Araç ödeme sonuçlanana kadar "sürüşte" kalıyor
`CompleteRideCommandHandler` artık aracı serbest bırakmıyor. Serbest bırakmak
son adımın işi.

### Telafi: ödeme reddedilirse
- Sürüş **geri alınmıyor.** Sürüş olmuş bir şey; silinmiyor, iptal edilmiyor.
- Sürüş "ödeme alınamadı" olarak işaretleniyor (`RidePaymentStatus.PaymentFailed`).
- Sürücü adına borç kaydı açılıyor (`OutstandingDebt`, sürüş başına en fazla bir).
- Araç **yine de** serbest bırakılıyor.

Telafi yazılmasaydı, reddedilen bir kart aracı sonsuza kadar "sürüşte"
bırakırdı; kimse kiralayamaz ve bunu ancak bir müşteri şikâyeti gösterirdi.

**Telafi ≠ geri alma.** Geri alma olmamış saymak; telafi, olmuş olanın üstüne
sistemi yeniden tutarlı hale getiren yeni bir işlem.

### Reddedilen ödeme bir hata değil, bir sonuç
Sağlayıcı "kart reddedildi" dediğinde ödeme adımı BAŞARIYLA bitiyor ve
`Success = false` olan bir sonuç olayı üretiyor. Yalnızca sağlayıcıya
ulaşılamaması istisna ve mesaj yeniden deneniyor. Ret bir hata olarak ele
alınsaydı, mesaj üç kez denenip ölü mektup kuyruğuna düşer ve araç hiç serbest
kalmazdı.

### Ödeme sağlayıcısına tekrar anahtarı
Tüketici ödemeyi isteyip sonucu kaydedemeden çökerse ödeme ikinci kez istenir.
Anahtar (sürüş kimliği) sayesinde sağlayıcı ikinci isteği ilkinin sonucuyla
cevaplıyor. Bizim tarafımızdaki tekrar kontrolü bu durumu yakalayamaz, çünkü
çökme "işlendi" kaydından önce oluyor.

### Webhook (70. gün)
- `POST /api/webhooks/payments`, kimlik doğrulaması yok; her istek **imzalı**:
  `HMAC-SHA256(sır, "{zaman damgası}.{ham gövde}")`, başlıklar
  `X-Scootly-Timestamp` ve `X-Scootly-Signature`.
- İmza **ham gövde** üzerinden: model bağlama gövdeyi yorumlayıp yeniden
  serileştirseydi baytlar değişebilirdi.
- **Zaman damgası imzanın içinde**, 5 dakikadan eskiyse ret: yakalanan geçerli
  bir istek sonsuza kadar tekrar oynatılamaz. Pencere içindeki tekrarları
  idempotency kaydı yakalıyor.
- Karşılaştırma sabit zamanlı (`CryptographicOperations.FixedTimeEquals`).
- Sır tanımlı değilse her istek reddediliyor (kapalı başarısızlık).
- Webhook sonucu **doğrudan işlemiyor**; aynı `payment.authorized` olayını
  outbox'a yazıyor. Sonucu işleyen mantık tek yerde (saga'nın son adımı).
- Tekrar gelen bildirim **200** alıyor: sağlayıcılar 2xx görene kadar yeniden
  gönderir, ikinci gelişe hata dönmek sonsuz bir döngü başlatırdı.

## Sonuçlar

**Olumlu**
- Ödeme hatasında araç kilitli kalmıyor.
- Aynı sonuç iki kez gelse de (kuyruk ya da webhook) ikinci bir borç ya da
  ikinci bir araç serbest bırakma yok.

**Olumsuz / bilinen açıklar**
- **Worker çalışmıyorsa araçlar "sürüşte" kalıyor.** Sürüş bitti, ama saga'nın
  son adımı çalışmadığı için araç haritaya dönmüyor. Bir izleme (ör. "5
  dakikadan uzun süredir ödeme bekleyen sürüş") yok; 22. haftanın arıza
  senaryosu tam olarak bunu gösterecek.
- Ödeme sağlayıcısı bugün **sahte** (`FakePaymentGateway`, kural tabanlı:
  `Payments:FakeDeclineAbove` üstü reddedilir). 71. günde gerçek simülatör ve
  asenkron sonuç (önce "beklemede", sonuç webhook ile) gelecek.
- Borcun kapatılması (sürücü kartını güncelledi, tekrar deneme) yok.
- Webhook gövdesi bizim tanımladığımız bir biçim; gerçek bir sağlayıcının
  biçimine uyarlanması gerekecek.
