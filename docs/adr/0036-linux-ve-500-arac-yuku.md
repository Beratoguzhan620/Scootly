# ADR 0036: Linux ve Sorun Giderme, 500 Arac Yuku (100. Gun)

## Durum
Kabul edildi.

## Olculenler
- Konteyner: `uid=1654(app)`, PID 1 `dotnet`, taban Ubuntu 24.04.5. `bash`, `sh` (dash), `ps`, `top` var; `curl` ve `wget` yok.
- 500 arac, kayit betigiyle (FleetManager yetkili kullanici) eklendi. Arac kaydinda benzersiz alan olmadigi icin betik
  once mevcut arac sayisini okuyup yalnizca eksigi ekler.
- Kayit ucunda kullanici basina istek siniri var: 30 istek gecti, sonra 429 geldi; 40 sn bekleme siniri acmadi.
  `RateLimiting__UserPerMinute=5000` ortam degiskeniyle kalan 458 arac 429 almadan eklendi (yalnizca yuk testi icin
  gecici Compose override'i; depoya girmez).
- Simulator 500 arayla 17 tur boyunca her turda 500 okumalik partiyi 5 sn aralikla gonderdi, hata yok
  (parti siniri 500 okuma, sinir dahil).
- Yuk altinda: Api ~%1 CPU / 170 MiB, Worker ~102 MiB, Postgres ~34 MiB, RabbitMQ bes ornekte %0,2-1,3 CPU
  (ilk ornekteki %140 tekrarlanmadi). Butun kuyruklarda mesaj 0, DLQ ve retry bos, outbox'ta bekleyen 0
  (toplam 77), alan gorevi 35. Son 3 dakikada Api logunda ERR/WRN yok.
- Korelasyon kimligi: yanit basligi gelen X-Correlation-Id'yi koruyor. Konsol sablonu ozellikleri yazmadigi icin
  `docker logs | grep <id>` bos donuyordu. Konsol sablonuna `{Properties:j}` eklenince Api logunda
  `{"CorrelationId": "...", ...}` goruldu.

- Hiz siniri politikalari dosyadan okundu (RateLimiting.cs, appsettings.json): hepsi sabit pencere, 1 dk, QueueLimit 0.
  Anonymous 60, Auth 10, User 30, Device 600, Webhook 300 istek/dk. Anahtarlar: Anonymous/Auth/Webhook IP,
  User ve Device kimlik (yoksa IP). Kayit ucu disindaki politikalar yuk altinda sinanmadi.

## Olculmeyenler
- Mvc ve Worker'a ayni konsol sablonu uygulandi ama yeniden derlenip denenmedi. Mvc'de `UseSerilogRequestLogging`
  CorrelationId'yi istek ozetine ekleyip eklemedigi bilinmiyor.
- Bir turda iki parti arasi 8 sn gecti (digerleri 5 sn); nedeni bulunmadi.
- Yuk altinda tek bir `docker stats --no-stream` ornegi RabbitMQ icin gurultuluydu; surekli bir olcum alinmadi.

## Karar
Konsol sablonu `{Properties:j}` icerir. Log satirlari uzar ama korelasyon kimligiyle arama konteyner loglarinda
calisir. Daha temiz alternatif JSON bicimi (Serilog.Formatting.Compact imajda mevcut); ayrica karara baglanmadi.