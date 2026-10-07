# Scootly: 15 dakikalik sunum provasi

Sure dagilimi bir plandir; kronometreyle prova edilmedi (110. gun). Provada her bolumun gercek suresini "Gercek" sutununa yazin ve gerekirse bolumleri kisaltin. Konusma notlari yalnizca repoda kayitli gercekleri icerir; kaynak sutunu nereden dogrulanacagini gosterir.

| Dk | Bolum | Gercek | Kaynak |
|---|---|---|---|
| 0-1 | 1. Problem | ___ | README Genel Bakis |
| 1-3 | 2. Mimari | ___ | system-design.md bolum 1 |
| 3-8 | 3. Bir surusun yolculugu | ___ | system-design.md bolum 2 |
| 8-10 | 4. Demo | ___ | README Calistirma |
| 10-12 | 5. Dayaniklilik | ___ | ADR 0042, 0044; RabbitMqRecoveryTests |
| 12-14 | 6. Riskler | ___ | system-design.md bolum 3 |
| 14-15 | 7. Kapanis | ___ | ADR 0044 |

## Konusma notlari

### 1. Problem (1 dk)
- Sehir ici elektrikli scooter paylasimi: arac rezerve edilir, surus yapilir, ucret tahsil edilir.
- Proje 22 haftalik .NET backend ogrenme planinin capstone calismasidir (ogrenme/portfoy projesi; gercek filo ve gercek odeme saglayicisi yok).
- Soylenecek tek cumle: "Amac ozellik sayisi degil; dagitik bir akisin (surus -> odeme) arizada da dogru kalmasi."

### 2. Mimari (2 dk)
- Clean Architecture katmanlari; 5 bounded context: Fleet, Riding, Telemetry, Geo, FieldOps.
- Surecler: Api, Mvc, Worker, PaymentSimulator, DeviceSimulator.
- Odeme ayri context degil, `Ride` icinde (ADR 0019, 0023); Wallet/Billing bilincli birakildi.
- Bir cumle: ayni islemde iki aggregate'i yalnizca surus bitirme degistirir (`Ride` + `Vehicle`), gerisi olaylarla baglanir.

### 3. Bir surusun yolculugu (5 dk, ana bolum)
Sirayla anlat, her adimda tek cumle:
1. Rezerve: arac `Reserved`, 10 dk; suresi dolarsa Worker dusurur.
2. Baslat: yalnizca rezervasyonu yapan surucu; `POST /api/rides/start`, 201.
3. Bitir: `POST /api/rides/{id}/complete`, **202 Accepted** (odeme bu cagrida alinmaz).
4. Tek `SaveChanges`: `Ride` + `Vehicle` + outbox satiri birlikte yazilir.
5. Outbox yayincisi satirlari RabbitMQ `scootly.events` exchange'ine yayinlar (en az bir kez teslim).
6. `RideChargeConsumer` odemeyi alir; saglayiciya idempotency anahtariyla gider.
7. Webhook: imzali, `EventId` ile tekrar yutulur.
8. Uzlastirma: Worker, `Pending` suruslari dakikada bir yeniden dener; 5 retle `Failed`.

Beklenen sorular ve cevaplar:
- "Neden 202?" Odeme gecici hata verebilir; surucuyu bekletmemek icin tahsilat asenkron, durum `payment-status` ile sorgulanir.
- "Mesaj iki kez gelirse?" `ChargeRideCommand` yalnizca `Pending` surusu tahsil eder, saglayici idempotency anahtarini tekrar etmez, webhook `EventId` ile yutulur.
- "RabbitMQ yokken surus biterse?" Olay outbox'ta birikir, RabbitMQ donunce yayinlanir (106. gun olculdu).

### 4. Demo (2 dk)
- On kosul: yigin onceden ayakta olsun (dev stack bu oturumda durduruldu, volume'lar duruyor; komutlar README'de). Calismazsa yedek ekran goruntusune gec.
- Akis: rezerve -> baslat -> bitir -> `payment-status` sorgula; Pending'den Paid'e gecisi goster.
- Yedek plan: demo 2 dk'yi asarsa durdur, bolum 3'teki akis zaten anlatildi.

### 5. Dayaniklilik (2 dk)
- 106. gun: kasitli ariza provalari (ADR 0042); RabbitMQ kesintisinden sonra tuketiciler toparlanmadi.
- 109. gun: kok neden bulundu (`RabbitMqConnectionProvider` kurtarilmakta olan baglantiyi dispose ediyordu), duzeltildi, `RabbitMqRecoveryTests` ile olculdu: duzeltme oncesi kirmizi, sonrasi yesil.
- Ders (tek cumle): ilk kontrol deneyi gecersizdi, cunku test yardimcisi saglayiciya kendisi dokunuyordu; kontrol deneyini once denetle.
- Dur ve durust ol: Compose/Nginx uzerinde provayi tekrarlamadik.

### 6. Riskler (2 dk)
En onemli dort: tek PostgreSQL, DLQ izleme/alarm yok, surec ici telemetri kuyrugu, prod'da gozlemlenebilirlik yok. Tam liste: system-design.md bolum 3 (13 madde).
- "Neyi olcmediniz?" system-design.md bolum 5: bulut karsiliklari, yolculugun bastan sona izlenmesi, yuk testi sonuclarinin belgeye tasinmasi, 81-108b notlari.

### 7. Kapanis (1 dk)
- Uc kayitli ders: (1) outbox + idempotency + uzlastirma birlikte calisir, tek basina hicbiri yetmez; (2) ariza provasi gercek hata buldu; (3) olculmeyeni acikca yazmak guvenilirligi arttirir.
- Sonraki adim: bkz. teknik borc "Acik kalanlar" (DLQ alarmi, prod gozlemlenebilirligi).

## Genel prova listesi

1. Bir kez sesli, kronometreyle bastan sona calis; "Gercek" sutununu doldur.
2. 15 dk'yi asarsan once demo, sonra bolum 6'yi kisalt; bolum 3'e dokunma.
3. "Neden mikroservis degil?" (ADR 0017 modular monolit degerlendirmesi) sorusunu da hazirla.
4. Slayt gerekiyorsa bu belge konusma notudur; slayt icin ayri talep edin.
