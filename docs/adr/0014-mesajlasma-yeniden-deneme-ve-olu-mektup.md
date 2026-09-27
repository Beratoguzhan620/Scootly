# ADR 0014: Mesajlaşma — Topoloji, Yeniden Deneme ve Ölü Mektup

## Durum
Kabul edildi (Hafta 13, 61–65. günler).

> Numara 0014: `berat` dalında 0011–0013 kullanılıyor. İki dal birleştiğinde
> çakışmasın diye atlandı.

## Bağlam
Sürüş bitişi artık üç işi senkron yapıyordu ve ücret hesabı bunlardan biriydi.
Hafta 13'ün hedefi bu işleri istekten ayırıp kuyruk üzerinden arka plana
taşımak. Kuyruğa geçmek üç yeni soru getiriyor:

1. Mesaj **nereye** gider — kim hangi olayı alır?
2. Tüketici başarısız olursa **ne olur** — kaç kez denenir, sonra?
3. Hiçbir denemenin düzeltemeyeceği bozuk bir mesaj (zehirli mesaj) **ana
   akışı nasıl tıkamaz**?

## Karar

### Topoloji

| Exchange | Tür | Anahtar | Ne için |
|---|---|---|---|
| `scootly.events` | topic | olay adı (`ride.completed`) | Yayınlanan her olay |
| `scootly.retry` | direct | kuyruk adı | Yeniden denenecek mesajlar |
| `scootly.dead` | direct | kuyruk adı | Artık denenmeyecek mesajlar |

Her tüketici kuyruğunun iki eşi var: `.retry` (kimse dinlemiyor, TTL 5 sn,
süresi dolunca ana kuyruğa döner) ve `.dlq` (kimse dinlemiyor, bir insanın
bakmasını bekler).

| Kuyruk | Olay | Tüketici | Sonuç |
|---|---|---|---|
| `scootly.fare-calculation` | `ride.completed` | `RideCompletedConsumer` | Sürüşe ücret yazılır |
| `scootly.field-tasks` | `vehicle.battery-low` | `BatteryLowConsumer` | Saha görevi açılır |

Topolojinin tamamı tek dosyada (`RabbitMqTopology`) ve **hem yayınlayıcı hem
tüketici** bildiriyor. RabbitMQ'da bağlı kuyruğu olmayan bir exchange'e gelen
mesaj sessizce atılır; kuyrukları yalnızca tüketici bildirseydi, Worker hiç
başlamamışken yayınlanan her olay iz bırakmadan kaybolurdu.

### Başarısızlık türleri

| Tür | Örnek | Ne olur |
|---|---|---|
| Zehirli (`Poison`) | JSON bozuk, zorunlu alan eksik, kimlik boş | Hemen `.dlq` |
| Reddedildi (`Rejected`) | Tüketici `Result.Failure` döndü (sürüş yok) | Hemen `.dlq` |
| Geçici (`Transient`) | Tüketici istisna fırlattı (veritabanı yok) | 5 sn sonra tekrar; 3. denemeden sonra `.dlq` |

Kural `DeliveryPolicy`'de, RabbitMQ'dan bağımsız ve birim testli.

### Yeniden deneme: bekleme kuyruğu, `requeue` değil
Başarısız mesajı `BasicNack(requeue: true)` ile kuyruğun başına geri koymak en
basit yol. Reddedildi: mesaj milisaniyeler içinde geri gelir. Hata geçici bir
kesintiyse (veritabanı yeniden başlıyor) kesinti o sürede geçmez, üç deneme
bir saniyenin altında tükenir ve kurtarılabilecek bir mesaj ölü kuyruğa düşer.
Bekleme kuyruğu her denemenin arasına 5 saniye koyuyor.

### Deneme sayacı: kendi başlığımız, `x-death` değil
RabbitMQ'nun ölü mektup mekanizması `x-death` başlığında bir sayaç tutuyor.
Reddedildi: içeriği RabbitMQ sürümleri arasında değişti ve yanlış okunursa
sayaç sessizce sıfırda kalır — mesaj sonsuza kadar denenir. Onun yerine
`scootly-attempt` başlığı; tüketici servisi mesajı bekleme kuyruğuna
yayınlarken sayacı kendisi artırıyor.

### Sıra: önce taşı, sonra onayla
Mesaj `.retry` ya da `.dlq`'ya **yayınlandıktan sonra** ana kuyruktan
onaylanıyor. Tersi sırada arada çökmek mesajı iki yerden de silerdi. Bu
sırayla en kötü durum iki kopya — en az bir kez teslimde zaten beklenen bir
durum.

## Sonuçlar

**Olumlu:**
- Bozuk bir mesaj ana kuyruğu tıkamıyor; ölü kuyrukta, sebebiyle birlikte
  (`scootly-failure-reason` başlığı) yönetim arayüzünden okunabiliyor.
- Kısa kesintiler (veritabanı, ağ) kendiliğinden atlatılıyor.
- RabbitMQ kapalıyken Worker çökmüyor, birkaç saniyede bir yeniden bağlanıyor.

**Olumsuz / bilinen açıklar:**
- **İkili yazma problemi çözülmedi.** `CompleteRideCommandHandler` sürüşü
  kaydedip ardından yayınlıyor. Yayınlama başarısız olursa sürüş tamamlanmış
  ama ücreti hiç hesaplanmayacak. Hata loglanıyor ama olay kayıp. Çözüm 66.
  günün outbox'ı.
- **Yayıncı onayı (publisher confirm) yok.** Yayınlama döndüğünde mesajın
  RabbitMQ'nun diskinde olduğu garanti değil. 67. günün outbox göndericisi
  "gönderildi" işaretini bu onaydan sonra koymalı.
- **Tekrar gelen mesaj tam korunmuyor.** Ücret tüketicisi "ücret zaten var mı"
  diye bakıyor; bu tek tüketiciyle yeterli, iki tüketici aynı mesajı aynı anda
  işlerse değil. 68. gün.
- **Ölü kuyruktaki mesajı geri göndermek elle.** Yönetim arayüzünden ya da
  `rabbitmqadmin` ile. Bir "tekrar oynat" aracı yok.
- Bekleme süresi değiştirilirse `.retry` kuyrukları silinip yeniden
  oluşturulmalı; RabbitMQ kuyruk argümanlarının sonradan değişmesine izin
  vermiyor (`PRECONDITION_FAILED`).

## Reddedilen alternatifler
- **Tüketicileri API sürecinde çalıştırmak.** API yatay ölçeklendiğinde
  tüketiciler de kopyalanır ve istek karşılayan süreç arka plan işi
  yüklenir. Tüketiciler Worker'da.
- **Domain olayını doğrudan yayınlamak.** Alan modelindeki her değişiklik
  kuyruğun öbür ucundaki tüketiciyi kırardı. Entegrasyon olayları ayrı tipler
  (`Application/IntegrationEvents`) ve çeviri tek yerde (`IntegrationEventMapper`).
- **RabbitMQ'nun gecikmeli mesaj eklentisi.** Bekleme kuyruğunun işini daha
  zarif yapar ama eklenti ayrı kurulum istiyor; TTL + ölü mektup çekirdek
  RabbitMQ ile çalışıyor.
