# Sistem tasarimi

Durum: 108. gunde bulut esleme eklendi; 110. gunde bounded context'ler, bir surusun yolculugu ve risk listesi eklendi. Bu belgedeki her iddia ya kodda okundu (dosya adi verildi), ya da "olculdu / olculmedi" olarak isaretlendi.

## 1. Bounded context'ler

Kod `Scootly.Domain` altinda su klasorlerde: `Fleet`, `Riding`, `Telemetry`, `Geo`, `FieldOps`. Sozluk: `domain-glossary.md`.

| Context | Sorumluluk | Ana tipler | Notlar |
|---|---|---|---|
| Fleet (Filo) | Arac kaydi, durum makinesi (Available, Reserved, InRide, Maintenance, Lost), batarya, rezervasyon | `Vehicle`, `BatteryLevel`, `VehicleModel` | Rezervasyon 10 dk, surucu basina bir aktif rezervasyon (`ReservationPolicy`). `Vehicle` hem filo bilgisini hem anlik operasyonel durumu tasiyor (ADR 0019 incelemesi). |
| Riding (Surus) | Surus yasam dongusu (Active, Completed, Abandoned), tarife, ucret, odeme durumu | `Ride`, `Tariff`, `PaymentStatus` | Odeme ayri context DEGIL, `Ride` icinde gomulu (ADR 0019, 0023). Wallet/Billing bilincli birakildi. |
| Telemetry | Cihazdan gelen konum/batarya okumalari, 30 gun saklama | `TelemetryReading` | Siradisi eski okumalar yok sayilir (ADR 0024). Okumalar surec ici kuyruktan gecer (ADR 0012). |
| Geo | Hizmet bolgesi, park yasagi bolgesi, geofence | `GeoPoint`, `ServiceArea`, `NoParkingZone`, `GeofenceEvaluator` | Canli bildirim gruplamasi icin kullaniliyor. Park yasagi surus bitirirken zorlanmiyor (teknik borc, bilincli birakildi). |
| FieldOps (Saha) | Saha gorevi: dusuk batarya / terk edilmis arac sonrasi operator is akisi, foto ile tamamlama | `FieldTask` | ADR 0025; foto icin nesne depolama ADR 0043. |

Destekleyici parcalar (context degil): kimlik ve yetkilendirme (`Infrastructure/Identity`, `Authorization`; ADR 0021, 0026-0028), mesajlasma (`Infrastructure/Messaging`: outbox, tuketici, idempotency), odeme istemcisi (`Infrastructure/Payments`), nesne depolama (`Infrastructure/Storage`).

Calisan surecler:

| Surec | Rol |
|---|---|
| `Scootly.Api` | HTTP API (`/api/...`, `/api/v{n}/vehicles`), SignalR `FleetHub`, telemetri kuyrugu tuketicisi; mesajlasma aciksa `RideChargeConsumer` ve `VehicleStatusNotificationConsumer` de burada calisir (`Program.cs`). |
| `Scootly.Mvc` | Web arayuzu; Application + Infrastructure'a bagimli, Api'ye referansi yok. |
| `Scootly.Worker` | Periyodik isler: `ReservationTimeoutService` (30 sn), `AbandonedRideDetector` (5 dk), `PendingPaymentRetryService` (1 dk), `BatteryThresholdScanner`, `DataRetentionService`; `BatteryLowConsumer` (FieldOps). Bugun tek ornek. |
| `Scootly.PaymentSimulator` | Odeme saglayicisi taklidi (hata orani ayarlanabilir, webhook gonderir). |
| `Scootly.DeviceSimulator` | Arac cihazi taklidi, telemetri gonderir. |

Context'ler arasi baglar: ayni islemde iki aggregate'i degistiren tek yer surus bitirme (`CompleteRideCommandHandler` hem `Ride` hem `Vehicle` kaydeder, tek `SaveChanges`). Geri kalan baglar olaylarla (outbox) kurulur: `RideCompleted`, `RideAbandoned`, `VehicleBatteryLow`, `VehicleStatusChanged` (`IntegrationEventNames`).

## 2. Bir surusun yolculugu

Rezervasyondan uzlastirmaya, kodda okunan adimlarla:

1. **Rezervasyon.** `POST /api/v{n}/vehicles/{id}/reserve`. Arac `Reserved` olur, `ReservedBy`/`ReservedAt` yazilir. Surucu 10 dk icinde baslatmazsa `ReservationTimeoutService` (30 sn'de bir) rezervasyonu dusurur (`ExpireReservationCommand`).
2. **Baslat.** `POST /api/rides/start` (`StartRideCommand`). Yalnizca rezervasyonu yapan surucu baslatabilir; arac `InRide`, surus `Active` olur. Yanit 201. Yakin arac onbellegi gecersiz kilinir.
3. **Telemetri (paralel akis).** Cihaz `POST /api/telemetry/batch` ile okuma yollar; okumalar surec ici `TelemetryChannel`'a girer, `TelemetryChannelConsumer` isler (`ProcessTelemetryBatchCommand`). Batarya esigi (%20) ilk kez asilinca `VehicleBatteryLow` olayi uretilir.
4. **Bitir.** `POST /api/rides/{id}/complete` (`CompleteRideCommand`). Surus `Ride.Complete` ile kapanir, ucret `Tariff.Standard` ile hesaplanir; arac `CompleteRide` ile `Available` olur. Tek `SaveChanges`; iyimser es zamanlilik (xmin) cakismasinda en fazla 3 deneme (`OptimisticConcurrency`). Yanit **202 Accepted**: odeme bu cagrida alinmaz, durum `Pending` doner.
5. **Outbox.** Ayni `SaveChanges` icinde alan olaylari `OutboxMessages` satirina cevrilir (`ScootlyDbContext` + `DomainEventOutboxMapper`). Is verisi ile olay ya birlikte yazilir ya hic yazilmaz.
6. **Yayin.** `OutboxPublisherService` 2 sn'de bir `ProcessedAt IS NULL` satirlari `FOR UPDATE SKIP LOCKED` ile ceker, publisher-confirm ile `scootly.events` (topic, kalici) exchange'ine yayinlar; baglanti hatasinda bekleme 60 sn'ye kadar buyur (`OutboxProcessor`, `OutboxPublisherService`). Teslim "en az bir kez"dir; tekrar teslim olabilir.
7. **Odeme tuketicisi.** `RideChargeConsumer`, `scootly.payments.ride-charges` kuyrugunda `RideCompleted` ve `RideAbandoned` dinler, `ChargeRideCommand` calistirir. Sonuclar: `Approved` -> surus `Paid`; `Declined` -> ret bir deneme olarak yazilir, mesaj Ack (sonraki denemeyi Worker yapar); `GatewayUnavailable` -> `Retry` (10 sn bekleyen retry kuyrugu, en fazla 3 yeniden deneme, sonra DLQ); surus bulunamazsa dogrudan DLQ. Saglayiciya her deneme `ride-{surus}-attempt-{n}` idempotency anahtariyla gider.
8. **Webhook.** Saglayici `POST /api/webhooks/payment-callback` cagirir. Govde 16 KB ile sinirli, imza dogrulanir (`PaymentWebhookValidator`; gecersiz/suresi gecmis imza 401). `EventId` ile idempotent islenir (`IdempotentMessageHandler`, `ProcessedMessages`). Basarili webhook surusu `Paid` yapar; basarisiz webhook bir sey degistirmez. Gec gelen onay, `Failed` bir surusu bile odenmis sayar (sozluk).
9. **Uzlastirma.** `PendingPaymentRetryService` dakikada bir, `Pending` olup son denemesi 10 dk'dan eski olan ya da hic denenmemis ve bitisi 5 dk'dan eski olan surusleri (en fazla 50) yeniden tahsil etmeyi dener. 5 reddedilmis denemede surus `Failed` olur ve operasyon ekibine devredilir.
10. **Terk edilen surus (alternatif son).** 2 saatten uzun `Active` kalan surusu `AbandonedRideDetector` (5 dk'da bir) kapatir, gecen sureyi ucretlendirir, araci `Maintenance`'a alir; `RideAbandoned` olayi ayni 5-9. adimlardan gecer.

Tekrar teslim neden guvenli: `ChargeRideCommand` yalnizca `PaymentStatus == Pending` olan surusu tahsil eder, saglayici idempotency anahtarini tekrar etmez, webhook `EventId` ile tekrarini yutar.

Kontrol akisinin ozeti:

```
rezerve -> baslat -> (telemetri) -> bitir (202)
   bitir: Ride + Vehicle + OutboxMessages  [tek SaveChanges]
   OutboxPublisher -> RabbitMQ scootly.events -> RideChargeConsumer -> ChargeRide -> saglayici
   saglayici -> webhook -> Paid        Worker -> uzlastirma (Pending'leri tekrar dener)
```

Olculenler: 106. gun ariza provalari (DB, Redis, RabbitMQ, odeme kesintisi), 105-107. gun yuk testi (`docs/experiments/load-test-2026-10-06.md`), 109. gun RabbitMQ kurtarma testi (Testcontainers). Bu bolumdeki zamanlama degerleri (2 sn, 10 sn, 10 dk...) kodda/varsayilan ayarda okunan degerlerdir; uretim yapilandirmasinda farkli olabilir, bu gun okunmadi.

## 3. Risk listesi

Kaynak: `docs/backlog/technical-debt.md` (109. gun son durum) ve 106-108b notlari. "Olculdu" yalnizca ilgili gun gercekten olculduyse yazilidir.

| # | Risk | Etki | Durum / kanit | Azaltma |
|---|---|---|---|---|
| 1 | Tek PostgreSQL | DB durunca API `/health/ready` 503, yazma yok | 106. gun olculdu (503). Yedek/replika/yedekten donus denenmedi. | Yonetilen PostgreSQL (bkz. bulut esleme), yedek ve geri yukleme tatbikati yapilmadi. |
| 2 | Surec ici telemetri kuyrugu | API yeniden baslarsa bekleyen okumalar kaybolabilir; her replika kendi kuyrugunu isler | ADR 0012; teknik borc, bilincli birakildi. | Kalici kuyruk ihtiyaci dogarsa RabbitMQ/stream. |
| 3 | Tek cihaz ag gecidi kimligi | Tek sir sizarsa tum cihazlar taklit edilebilir | ADR 0021; bilincli birakildi. | Arac basina kimlik (provizyon gerektirir). |
| 4 | DLQ izleme / alarm yok | Odeme mesaji DLQ'ya dusunce kimse fark etmeyebilir, DLQ'dan yeniden surme yok | Acik; 107. gun: Prometheus'ta alarm kurali yok. 106. gun: odeme kesintisinde ~31 sn'de DLQ'ya dusus olculdu. | Alarm kurali + yeniden surme araci; su an `PendingPaymentRetryService` Pending suruslere ikinci bir guvenlik agi. |
| 5 | Tek RabbitMQ dugumu | Kesintide olaylar outbox'ta birikir, tuketiciler durur | 106. gun: outbox birikti. Tuketici toparlanma hatasi 109. gunde duzeltildi (testle olculdu); Compose/Nginx uzerinde tekrar olculmedi. | Yonetilen kuyruk; 106 provasi tekrarlanmali. |
| 6 | Mvc: Redis kapaliyken (yeniden) baslayan replika 500 veriyor | Redis kesintisi + deploy birlikte olursa arayuz acilmaz | 106. gun olculdu; mevcut replikalar calismaya devam etti. | Data Protection anahtar halkasinin Redis disina tasinmasi degerlendirilebilir (yapilmadi). |
| 7 | Docker healthcheck yalnizca `/health/live` | Bagimlilik kesintisinde konteyner `healthy` kaliyor | 106. gun olculdu. | `/health/ready` LB icin tehlikeli olabilir (hepsini havuzdan cikarir; LB davranisi olculmedi), ayri karar gerekir. |
| 8 | Tekrar teslim edilen `RideCompleted` retry hakkini tuketiyor | Gercek hata icin kalan deneme azalabilir | Teknik borc, acik. | Idempotency kontrolunu retry sayacindan ayirmak. |
| 9 | Prod'da gozlemlenebilirlik yok | Metrik/iz/log toplanmiyor | 107. gun: `observability` profili prod yiginda yok. | OTLP uyumlu yonetilen hizmet (ADR 0030). |
| 10 | Nesne deposu prod'a bagli degil | Foto yukleme prod'da calismaz (nginx 1 MB siniri, on-imzali URL ana makinesi) | 108b, acik. | Prod'a baglarken en az yetkili kimlik, hazir bucket, icerik taramasi. |
| 11 | Worker tek ornek | Worker dusunce uzlastirma, terk tespiti, rezervasyon dusurme durur | Replika/otomatik yeniden baslatma olculmedi. | Orkestrasyon + heartbeat saglik kontrolu (var). |
| 12 | Eski sirlar Git gecmisinde | Gecmis yeniden yazilmadi | 27.09.2026'da yenilendi (rotasyon); postmortem. Sizinti suresi yalnizca ust sinirla biliniyor. | CI'da gitleaks yok; GitHub push protection acik ama sizintiyla sinanmadi. |
| 13 | 81-108b "olculmedi / denenmedi" notlari | Bu belgenin guvence siniri | `technical-debt.md` gecmis kayitlari; toplu siniflandirma yapilmadi. | Her not kendi gununde. |

## 4. Bulut esleme tablosu

Bu tablo yon gosterir. Hicbir bulut hesabinda kurulum yapilmadi; maliyet, gecis suresi ve performans olculmedi.

| Bugun (compose) | Bulut karsiligi (kategori) | Tasimada dikkat edilecekler |
|---|---|---|
| PostgreSQL | Yonetilen PostgreSQL | Postgres'e ozgu ozellikler kullaniliyor (xmin eszamanlilik belirteci, kismi benzersiz indeks); Postgres uyumlu olmayan bir motora gecilmez. 106. gun: DB durunca api /health/ready 503 verdi (olculdu). |
| Redis | Yonetilen onbellek | Mvc Data Protection anahtar halkasi, oturum ve dagitik onbellek Redis'te; salt onbellek gibi dusunulmemeli. Redis durunca mevcut replikalar calisti (Degraded), yeni Mvc replikasi 500 verdi (106. gun olculdu). Anahtar halkasi kaybinin cerezlere etkisi olculmedi. |
| RabbitMQ | Yonetilen AMQP 0-9-1 kuyrugu (RabbitMQ uyumlu) | Topic exchange scootly.events, routing key = olay turu, kalici kuyruklar, retry/DLQ ve iki exclusive auto-delete kuyruk kullaniliyor. SQS/SNS gibi AMQP olmayan hizmetler dogrudan karsilik olmaz. 106. gun: baglanti kopmasindan sonra tuketicilerin geri gelmemesi gozlendi; 109. gunde kok neden bulundu ve duzeltildi (Testcontainers testiyle olculdu); tasimadan once Compose/Nginx uzerinde yeniden olculmeli. |
| Nginx | Yuk dengeleyici (TLS sonlandirma) | Bugun tek giris 127.0.0.1:8443, api ve mvc icin ikiser replika, mavi-yesil gecis nginx reload ile (ADR 0041). Bulutta hedef grubu/agirlik gecisine donusur. Saglik kontrolu icin /health/live daha guvenli aday: /health/ready bagimlilik kesintisinde 503 verir (olculdu) ve LB hepsini havuzdan cikarabilir (LB davranisi olculmedi). |
| GHCR | Container registry | CI/is akisi dosyalarinda ghcr.io gecer; yayin adiminin gercekten calistigi bu belgede dogrulanmadi. |
| Seq / Jaeger / Prometheus / Grafana / OTel collector | Yonetilen gozlemlenebilirlik (OTLP kabul eden) | ADR 0030: uygulama OTLP ile collector'a yazar; arka uc degisimi esas olarak collector yapilandirmasidir. Prod yiginda observability profili yok, Prometheus alarm kurali yok (teknik borc, 107. gun). |
| api / mvc / worker konteynerleri | Konteyner calistirma (orkestrasyon) | Worker saglik kontrolu heartbeat dosyasi; worker bugun tek ornek. Replika/otomatik yeniden baslatma ayarlari olculmedi. |
| .env.prod dosyasi | Gizli yonetim servisi | 27.09.2026 olayi (postmortems/2026-09-27-sizmis-sirlar.md) ve rotasyon proseduru (incident-response.md bolum 6, denenmedi) bu gecisin gerekcesi. |
| SeaweedFS (S3 uyumlu, AWSSDK.S3) | Nesne depolama (S3 uyumlu) | Gelistirmede var (compose profili `storage`, ADR 0043). Prod'a baglanmadi (technical-debt.md, 108b). Kod S3 API ile yazildi, buluta gecisin esas olarak yapilandirma olmasi beklenir; bulutta olculmedi. |

## 5. Olculmeyenler

- Hicbir hizmetin bulut karsiligi denenmedi; esleme tablosu bilgiye dayali.
- Dev compose'daki Redpanda bu belgeye alinmadi (rolu bu belgede dogrulanmadi).
- Maliyet, gecis suresi, ag gecikmesi ve yonetilen hizmet sinirlari olculmedi.
- Surus yolculugu bolumundeki akis kodun okunmasina dayanir; 110. gunde bastan sona yeniden calistirilip izlenmedi (Jaeger izi ya da log ile dogrulanmadi).
- Yuk testi sonuclari bu belgeye tasinmadi; `load-test-2026-10-06.md` ve ADR 0039'a bakin.
- `OutboxPublisherService`, `AddScootlyInfrastructure` icinde mesajlasma aciksa kaydedilir; Api, Worker ve Mvc ucu de bu kaydi cagirdigi icin ayni anda birden cok yayinci calisabilir (`FOR UPDATE SKIP LOCKED` ile cakisma onlenir, kodda okundu). Cok yayincili calismanin siralamaya etkisi olculmedi.
