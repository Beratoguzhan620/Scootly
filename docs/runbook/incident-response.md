# Olay Mudahalesi Runbook'u (107. Gun)

Bu belge, Scootly yiginda bir olay (kesinti, sir sizintisi, tutarsiz odeme) oldugunda ne yapilacagini tarif eder. **Durum notu:** teshis komutlari ve senaryo kartlarindaki davranislar 106. gunde olculdu (`failure-drills.md`, ADR 0042). Roller, onem seviyeleri, hedef sureler ve iletisim bolumleri **oneridir; hic tatbikat yapilmadi**. Calisan bir alarm sistemi yok (bkz. 9); olaylar bugun elle fark edilir.

## 1. Kapsam ve roller (oneri)

- Ekip: Berat ve Emre. Olay komutani: olayi ilk fark eden kisi. Ikinci kisi varsa teshis ve kaydi ustlenir; tek kisiysen once zarari durdur, sonra yaz.
- Iletisim kanali bu belgede tanimli degil (ekip karari bekliyor).

## 2. Onem seviyeleri (oneri)

| Seviye | Ornek | Ilk yanit hedefi |
|---|---|---|
| SEV1 | Sir sizintisi; yanlis ya da cift tahsilat; Api ve Mvc tamamen erisilemez | Hemen |
| SEV2 | Tek bagimlilik kesintisi (PostgreSQL, RabbitMQ, Redis, odeme gateway'i); bir kopyanin kaybi | 1 saat |
| SEV3 | Kismi bozulma; DLQ'da biriken mesaj; tek kopya hatasi | Sonraki is gunu |

Hedef sureler olculmedi, ekibin kapasitesine gore ayarlanmali.

## 3. Ilk 15 dakika

1. UTC zaman damgasini ve belirtiyi yaz: ne gorunuyor, kim fark etti.
2. Kapsami daralt: `docker ps --filter name=scootly --format "table {{.Names}}\t{{.Status}}"`. Docker `healthy` bagimlilik durumunu GOSTERMEZ: healthcheck `/health/live` kullanir (ADR 0042). Bagimlilik icin `/health/ready` bak.
3. Readiness (prod yigini, `deploy/` dizininden): `docker compose -p scootly-prod --env-file .env.prod -f docker-compose.yml -f docker-compose.prod.yml exec -T nginx wget -S -O- -T 15 http://api:8080/health/ready`. Mvc icin `Host: localhost` basligi gerekir (basliksiz 400): `--header "Host: localhost"` ve `http://mvc:8080/health/ready`. UYARI: RabbitMQ kesintisinde `/health/ready` baglantiya dokunur ve tuketicilerin kaybolmasina yol acabilir (bkz. 5.2); kesinti sirasinda gereksiz sorgulama.
4. Loglar: `docker logs --since 15m <konteyner>`; hata satirlari icin `ERR`/`WRN` ile filtrele. Seq yalnizca `observability` profilinde calisir (bkz. 9).
5. Zarari durdur; geri alinamaz islemden once yaptigini kaydet.
6. Olay notunu baslat (bkz. 7).

## 4. Teshis komutlari (106. gunde calistirildi)

- Kuyruk tuketicileri: `docker exec scootly-rabbitmq rabbitmqctl list_queues name messages consumers`. Beklenen (saglikli): `scootly.payments.ride-charges` 2 tuketici, `scootly.fieldops.battery-low` 1, iki `amq.gen-*` 1'er; `.retry` ve `.dlq` 0 mesaj.
- Baglantilar: `docker exec scootly-rabbitmq rabbitmqctl list_bindings source_name destination_name routing_key` (5 `scootly.events` satiri beklenir).
- Outbox bekleyen satir (konteyner `scootly-postgres`, kullanici `postgres`, veritabani `scootly`): `SELECT count(*) FROM "OutboxMessages" WHERE "ProcessedAt" IS NULL;`
- Odeme durumu dagilimi: `SELECT "PaymentStatus", "PaymentAttempts", count(*) FROM "Rides" GROUP BY 1,2;` (benzer sorgular 106. gunde calistirildi).
- PowerShell 5.1: SQL'i stdin ile ver (`'sorgu' | docker exec -i scootly-postgres psql -U postgres -d scootly`), JSON govdesini dosyadan gonder.

## 5. Senaryo kartlari (106. gunde olculen davranislar)

### 5.1 PostgreSQL kapali
- Belirti: Api ve Mvc `/health/ready` 503; istekler genel ProblemDetails ile 500 (ic detay yok); Worker "Outbox yayin turu basarisiz; ... tekrar denenecek" loglar, `healthy` kalir.
- Kurtarma: `docker start scootly-postgres`. Api 3 sn, Mvc 1 sn icinde toparlandi; ek adim gerekmedi.

### 5.2 RabbitMQ kapali
- Belirti: Api `/health/ready` 503 (RabbitMQ kontrolu `Unhealthy`); `/api/v1/vehicles` 200; outbox satirlari `ProcessedAt IS NULL` olarak birikir (`Attempts` ve `LastError` degismez).
- Kurtarma: `docker start scootly-rabbitmq`; birikenler 12 sn'de yayinlandi. Sonra tuketicileri kontrol et (bolum 4). Tuketici sayilari beklenenden azsa: `docker compose ... restart worker`, sonra `restart api`, sonra `exec -T nginx nginx -s reload`. Olculen: Worker restart'i `battery-low` tuketicisini, Api restart'i `ride-charges` tuketicilerini ve `VehicleStatusChanged` aboneliklerini geri getirdi; Mvc restart'i gerekmedi. Her restart ~6 sn'de `healthy` oldu.
- Dikkat: tuketiciler yalnizca kesintide baglantiya hic dokunulmadiginda kendiliginden dondu (13 sn). Nedeni dogrudan gozlenmedi (hipotez: ADR 0042). Duzeltilmedi.

### 5.3 Odeme gateway'i kesintisi
- Belirti: Api/Worker log'unda `OnRetry`, `OnCircuitOpened` (Polly); taze surusler retry kuyrugundan ~31 sn'de `.dlq`'ya duser, satirlar `Pending` kalir.
- Kurtarma: gateway donunce Worker uzlastirmasi (1 dk aralik) uygun `Pending` satirlari tahsil eder (eski satirlarda <=30 sn'de olculdu). DLQ'daki mesajlari yeniden suren bir mekanizma yok: `rabbitmqctl list_queues` ile `.dlq` sayisina bak, elle incele. Uzlastirma esik degerleri olculmedi.

### 5.4 Redis kapali
- Belirti: Api ve Mvc `/health/ready` 200 ama govde `Degraded`; Api calismaya devam eder; log'da `RedisConnectionException`.
- Kurtarma: `docker start scootly-redis`; 5-7 sn'de toparlandi, restart gerekmedi.
- Dikkat: Redis kapaliyken yeniden baslatilan ya da yeni acilan Mvc kopyasi 500 verir (DataProtection key ring okunamiyor). Redis kesintisinde Mvc'yi yeniden baslatma ya da dagitim yapma.

### 5.5 Kotu dagitim
- Geri alma: `docs/runbook/deployment.md` bolum 8 (blue-green geri alma) ve bolum 5 (geri alma secenekleri).

### 5.6 Worker durdu
- `docker compose ... restart worker` (olculen: ~6 sn'de `healthy`). Worker healthcheck'i yalnizca heartbeat dosyasidir; bireysel islerin sagligini kanitlamaz (ADR 0031).

## 6. Sir sizintisi (SEV1; oneri, adimlar bu belgede denenmedi)

27.09.2026'daki rotasyonun adimlari kayda gecmedi; asagidaki liste ADR 0021 ve `deploy/.env.example` anahtarlarina dayanan bir oneridir.

Sir envanteri (adlar, degerler degil): `JWT_KEY`, `JWT_HUB_KEY`, `APP_DB_PASSWORD`, cihaz sirri (DeviceAuth), `PAYMENT_WEBHOOK_SECRET`, `POSTGRES_PASSWORD`, RabbitMQ parolasi, Redis parolasi, `BOOTSTRAP_FLEET_MANAGER_PASSWORD`, `GRAFANA_ADMIN_PASSWORD`.

1. Neyin, nerede acildigini belirle (commit, log, ekran goruntusu); ilk gectigi commit'i bul.
2. Sirri GECERSIZ kil: yenisini uret (en az 32 karakter, ADR 0021).
3. `deploy/.env.prod` / user-secrets'i guncelle. Degerleri sohbete, commit'e ya da log'a yazma.
4. Servisleri yeni degerle yeniden olustur: `docker compose ... up -d --force-recreate <servis>`. JWT anahtari degisince tum token'lar gecersiz olur (beklenen).
5. PostgreSQL, RabbitMQ ve Redis parolalari icin sunucu tarafinda da degistir; yalnizca ortam degiskenini degistirmek yetmez. Bu komutlar burada denenmedi.
6. Git gecmisi: `git filter-repo` paylasilan dallari bozar (ADR 0021); rotasyon zorunlu ve yeterli, temizlik ekip karari.
7. Kotu kullanim izi ara: beklenmedik token/kullanici, odeme ve webhook kayitlari.
8. Postmortem yaz (bolum 8).

Onleme: `.env` ve `.env.prod` asla commit edilmez; yeni sirlar en az 32 karakter; Options + `ValidateOnStart` ile eksik sir uygulamayi acmaz. Acik: otomatik secret taramasi yok (postmortem aksiyonu).

## 7. Kayit

Olay notu icin tablo: `UTC saat | ne oldu / ne yapildi | kim`. Harici kullaniciya duyuru kanali bu belgede tanimli degil.

## 8. Olay sonrasi

- SEV1 ve SEV2 olaylar icin blameless postmortem: `docs/runbook/postmortem-template.md`; oneri: olaydan sonra 5 is gunu icinde. Aksiyonlar `docs/backlog/technical-debt.md`'ye yazilir.
- Ornek: `docs/runbook/postmortems/2026-09-27-sizmis-sirlar.md`.

## 9. Bilinen bosluklar (olculen / okunan)

- Alarm yok: `deploy/prometheus/` altinda `alert:`, `groups:`, `rule_files` eslesmesi bulunmadi (107. gun). Grafana panosu var (`deploy/grafana/dashboards/scootly-overview.json`).
- Seq, Jaeger, OpenTelemetry Collector, Prometheus ve Grafana yalnizca `observability` profilinde; 106. gunde calisan prod yiginda yoktu.
- Docker healthcheck'i yalnizca `/health/live`.
- DLQ yeniden surme ve DLQ alarmi yok.
- CI'da yalnizca paket zafiyet taramasi var (`ci.yml`, "Check vulnerable packages"); secret taramasi yok.
- Bu belgedeki surec bolumleri (1, 2, 6, 7, 8) hic denenmedi.

## 10. Olculmeyenler

- Bu belgedeki hedef sureler ve rol dagilimi.
- Sir rotasyon adimlari (PostgreSQL, RabbitMQ, Redis parola degisikligi).
- Giris yapmis Mvc oturumlarinin Redis kesintisindeki davranisi ve nginx'in 500 donen kopyaya trafik yollayip yollamadigi (bkz. ADR 0042).
