# Kasitli Ariza Provalari (106. Gun)

Bu belge, calisan prod yiginda (proje `scootly-prod`, blue, 2 Api + 2 Mvc + Worker, yerel Docker Desktop) kasitli olarak olusturulan dort arizayi ve gozlenen davranisi kaydeder. Her kosul tek kez calistirildi; sureler yoklama cozunurlugundedir (2-3 sn, bazi yerlerde 30 sn). Beklenen davranislar planin 106. gun maddesinden alinmistir. Kodda bu gun hicbir degisiklik yapilmadi. "Olculmeyenler" bolumu kasitli olarak ayridir.

## 1. Ozet

| Ariza | Plandaki beklenti | Olculen | Sonuc |
|---|---|---|---|
| PostgreSQL durdur | `/health/ready` 503, istekler 500 ProblemDetails (ic detay yok), Worker hata loglar ve devam eder | Api ready 503 (11,6 sn), Mvc ready 503 (4,1 sn); `/api/v1/vehicles` 500 genel ProblemDetails, ic detay sizmadi; Worker "Outbox yayin turu basarisiz; 8 sn sonra tekrar denenecek" loglayip `healthy` kaldi; geri gelince Api 3 sn, Mvc 1 sn, vehicles 1 sn | Uyuyor |
| RabbitMQ durdur | Outbox satirlari birikir, tuketiciler geri cekilmeyle yeniden baglanir, birikenler yayinlanir | Outbox satirlari birikti (`Attempts` 0, `LastError` bos), broker donunce 12 sn'de yayinlandi; tuketiciler yalnizca kesinti sirasinda baglantiya hic dokunulmadiginda kendiliginden dondu (13 sn), aksi halde donmedi (bkz. 4) | Tuketiciler icin UYMUYOR |
| Odeme kesintisi (`OutagePercent=100`) | Devre kesici acilir, `GatewayUnavailable` retry kuyruguna gider, Worker uzlastirmasi sonra tahsil eder | Devre kesici acildi; taze surusler retry kuyrugundan ~31 sn'de DLQ'ya dustu ve gateway donunce de `Pending` kaldi; eski `Pending` satirlar uzlastirmayla gateway donunca <=30 sn'de `Paid` oldu | Kismen uyuyor |
| Redis durdur | Api calisir, ready `Degraded` ama 200; Mvc oturumu kaybolur; DataProtection Redis'teyse dikkat | Ready 200 (`Degraded`), vehicles 200, mvc giris sayfasi 200; kesinti sirasinda yeniden baslatilan Mvc replikasi 500 verdi (key ring okunamadi); Redis donunce 5-7 sn'de toparlandi | Uyuyor; DataProtection riski yeni replikada dogrulandi |

## 2. Yontem ve notlar

- Komutlar `deploy/` dizininden: `docker compose -p scootly-prod --env-file .env.prod -f docker-compose.yml -f docker-compose.prod.yml ...`.
- Probe: nginx konteynerinden `wget -S http://api:8080/health/ready`; Mvc `/health/ready` icin `Host: localhost` basligi gerekir (basliksiz 400 dondu; nedeni koddan dogrulanmadi).
- Docker healthcheck'i `/health/live` kullanir ve bagimlilik gormez: tum kesintilerde konteynerler `healthy` kaldi. Bagimlilik durumu yalnizca `/health/ready`'de gorunur.
- Test verisi: `DriverId = 00000000-0000-0000-0000-000000000106` isaretli `Rides` satirlari ve `EventType = 'Drill106NoRoute'` olan `OutboxMessages` satirlari. Hepsi silindi; kuyruklar 0'a dondu.
- Odeme simulatoru `/api/failure-rate` ucunu yalnizca `Development`'ta acar; deneyde gecici bir compose override'i (`ASPNETCORE_ENVIRONMENT=Development`) kullanildi, sonra Production'a donuldu.
- PowerShell 5.1: JSON govdesi dosyadan (`docker cp` + `wget --post-file`), psql komutlari stdin ile verildi.
- `docker stop` Docker DNS kaydini siler: Postgres ve RabbitMQ icin hata turu "Name or service not known" (DNS) oldu. Bu, takili kalan ya da baglantiyi reddeden bir sunucuyu taklit etmez.

## 3. PostgreSQL

- Postgres durunca Api `/health/ready` 503 dondu (ilk kosuda 5 sn'lik zaman asimina takildi, ikinci kosuda `-T 40` ile 11,6 sn'de 503 olculdu); Mvc 4,1 sn'de 503.
- `/api/v1/vehicles` nginx uzerinden 500 dondu; govde genel ProblemDetails ("Beklenmeyen hata", `traceId`); `Npgsql`, `Exception`, `at Scootly`, `StackTrace` desenleri govdede yoktu.
- Worker log'unda `OutboxProcessor.PublishPendingAsync` icinde Npgsql baglanti hatasi ve "Outbox yayin turu basarisiz; 00:00:08 sonra tekrar denenecek" satiri var; Worker konteyneri `healthy` kaldi.
- `docker start scootly-postgres` sonrasi: ilk kosuda Api ve vehicles 4 sn, ikinci kosuda Api 3 sn, Mvc 1 sn, vehicles 1 sn. Uygulama konteynerleri yeniden baslamadi.

## 4. RabbitMQ

Kosullar (her biri tek kez, broker 45-57 sn kapali):

| Kosul | Sonuc |
|---|---|
| 5 outbox satiri eklendi, kesintide Api ve Mvc `/health/ready` cagrildi | 240 sn icinde tuketici donmedi: `ride-charges` 0, `battery-low` 0, `amq.gen-*` kuyruklari ve `VehicleStatusChanged` binding'leri kayboldu (3 binding kaldi) |
| Outbox bos, kesintide yalnizca Api `/health/ready` cagrildi | 150 sn sonra `ride-charges` 1 (olmasi gereken 2), `battery-low` 1, tek `amq.gen-*` 1, 4 binding |
| Outbox bos, kesintide hicbir probe yok | 13 sn'de 2/1/1/1 tuketici, 5 binding; restart gerekmedi |

- Outbox satirlari kesinti boyunca `ProcessedAt = NULL` kaldi; `Attempts` 0, `LastError` bos (planla uyumlu: baglanti kurulamayinca tur baslamiyor). Broker `healthy` olduktan sonra satirlar 12 sn'de islendi. Log satiri: "Outbox yayin turu basarisiz; 00:00:02 sonra tekrar denenecek" (Api ve Worker); log'daki bekleme degerinin buyudugu gorulmedi.
- Kesintide Api `/health/ready` 503 (RabbitMQ kontrolu `Unhealthy`, 4,4 sn), `/api/v1/vehicles` 200 kaldi. Mvc ready bir kosuda 503, digerinde 200 dondu; nedeni belirlenemedi.
- Kod okumasi: `RabbitMqConnectionProvider` `AutomaticRecoveryEnabled = true` ile baslar, ama `GetConnectionAsync` baglanti `IsOpen` degilse (otomatik kurtarma sirasinda da) eski baglantiyi dispose edip yenisini acar; `RabbitMqConsumerService` baglantiyi yerel degiskende tutar ve yalnizca `channel.IsClosed && connection.IsOpen` ise kanali yeniden kurar. `GetConnectionAsync`'i kesinti sirasinda cagiran yerler: `InfrastructureHealthChecks` (readiness) ve `OutboxProcessor`. Uc kosul bu okumayla uyumlu, ama mekanizma dogrudan gozlenmedi (hipotez).
- Hangi servis hangi tuketiciyi tutuyor (restart ile olculdu): Worker restart'i `battery-low` tuketicisini, Api restart'i `ride-charges` tuketicilerini ve iki `amq.gen-*` (`VehicleStatusChanged`) kuyrugunu geri getirdi; Mvc restart'i kuyruk listesini degistirmedi. Her restart ~6 sn'de `healthy` oldu.

Kurtarma (olculen): broker'i baslat; `docker exec scootly-rabbitmq rabbitmqctl list_queues name consumers` ile `ride-charges` 2, `battery-low` 1, iki `amq.gen-*` 1 beklenir; degilse `docker compose ... restart worker`, `restart api`, `exec -T nginx nginx -s reload`.

## 5. Odeme kesintisi

- Simulator `OutagePercent=100`: her Api replikasinda 2 `OnCircuitOpened`, 1 `OnCircuitHalfOpened`, 5 `OnRetry` (Polly, `IPaymentGateway-payment-pipeline`).
- 6 taze surus (`EndedAt = now`) icin `RideCompleted` yayinlandi: 10 sn sonra `.retry`'de 6, ~31 sn sonra `.dlq`'da 6; satirlar `Pending`, `PaymentAttempts = 0`. Simulator `0/0`'a alindiktan sonra 2,5 dk icinde degisiklik olmadi; DLQ'daki mesajlari yeniden suren bir mekanizma yok. Ayni surusun deneme zamanlari ~10 sn arayla (retry TTL yaklasik 10 sn, cikarim).
- `PendingPaymentRetryService` (Worker, 1 dk aralik): `PaymentStatus = Pending` ve (`LastPaymentAttemptAt` dolu ve `retryBefore` oncesi ya da bos ve `EndedAt < neverAttemptedBefore`) olan satirlari `EndedAt` sirasiyla `BatchSize` kadar alir. Esik degerleri okunmadi.
- Uzlastirma deneyi: 4 eski satir (`EndedAt` 1 gun once, `Pending`), mesaj yayinlanmadan, kesintide: Worker gateway'e gitti (503, Polly retry); satirlar `Pending`, `PaymentAttempts = 0`, `LastPaymentAttemptAt` bos kaldi (gateway'e ulasilamamasi deneme sayilmiyor). Simulator `0/0` olunca ilk yoklamada (<=30 sn) 4/4 `Paid`, `PaymentAttempts = 1`; Worker log'unda `OnCircuitHalfOpened`, istek 200, `OnCircuitClosed`.

## 6. Redis

- Redis kapaliyken Api-1, Api-2 ve Mvc-1 `/health/ready` 200 (govde `Degraded`); `/api/v1/vehicles` 200; Mvc-1 `/` 302, giris sayfasi 200; log'larda `RedisConnectionException` (5000 ms backlog zaman asimi).
- Redis kapaliyken `mvc-2` yeniden baslatildi: `/health/ready` 500, `/` 500, giris sayfasi 500; log'da "An error occurred while reading the key ring" (DataProtection) ve Redis baglanti hatasi; Docker bir sure `unhealthy` gosterdi (sonradan `healthy`).
- Redis baslatilinca Api-1 ve Mvc-1 5 sn, Mvc-2 7 sn'de ready 200; restart gerekmedi.
- Mvc, DataProtection anahtar halkasini, oturumu ve dagitik onbellegi Redis'te tutar (Gun 105 degisikligi, bkz. ADR 0041).

## 7. Olculmeyenler

- Postgres: kesinti sirasinda outbox satiri uretilmedi; geri geldikten sonra Worker'in yayin log'u okunmadi; hata turu DNS idi (bkz. 2).
- RabbitMQ: gercek `RideCompleted` olaylarinin kesintideki akibeti; tuketicilerin 240 sn sonrasi; log'daki sabit 2 sn bekleme degerinin nedeni; Mvc ready'nin iki kosuda farkli donmesinin nedeni; hipotezin dogrudan kaniti (ornegin `GetConnectionAsync` icin sayac/log).
- Odeme: uzlastirma esik degerleri (`neverAttemptedBefore`, `retryBefore`); taze surusun uygun hale gelme suresi; DLQ'daki mesajlarin akibeti; devre kesici esik ve sure ayarlari.
- Redis: giris yapmis bir kullanicinin oturumu; Redis'te anahtar kaybolursa mevcut cerezlerin cozulememesi; Redis kapaliyken sayfa gecikmesi; nginx'in 500 donen replikaya trafik yollayip yollamadigi; `DBSIZE` 1 kaldi ama anahtarin ayni olup olmadigina bakilmadi.
- Genel: her kosul tek kez; yerel Docker Desktop (Linux VM) davranisi bulut ortamini temsil etmeyebilir.
