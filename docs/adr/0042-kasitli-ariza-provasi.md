# ADR 0042: Kasitli Ariza Provasi Bulgulari (106. Gun)

## Durum
Kabul edildi. Bulgular kaydedildi, kod degisikligi yapilmadi; duzeltmeler acik (bkz. teknik borc).

## Karar
- Prod yiginda dort ariza (PostgreSQL, RabbitMQ, odeme gateway'i, Redis) kasitli olarak olusturuldu; yontem, sureler ve sonuclar `docs/runbook/failure-drills.md`'de.
- Bu gun kod degistirilmedi: bulgular ADR'ye ve teknik borca yazildi, duzeltme karari ayri bir degisikliktedir.
- Docker healthcheck'i `/health/live` olarak kalir (bagimliliklara bakmaz); bagimlilik durumu `/health/ready`'de izlenir.

## Olculenler
- PostgreSQL kesintisinde `/health/ready` 503, istekler genel ProblemDetails ile 500; Worker calismaya devam etti; geri gelince 1-4 sn'de toparlandi.
- RabbitMQ kesintisinde outbox satirlari birikti ve broker donunce 12 sn'de yayinlandi; ancak tuketiciler kesinti sirasinda baglantiya dokunuldugunda (outbox satiri ya da `/health/ready` cagrisi) yeniden baglanmadi (0 tuketici, 240 sn); hic dokunulmadiginda 13 sn'de kendiliginden dondu. Worker ve Api restart'i tuketicileri geri getirdi.
- Odeme kesintisinde devre kesici acildi; taze surusler ~31 sn'de DLQ'ya dustu; eski `Pending` satirlar gateway donunce Worker uzlastirmasiyla <=30 sn'de `Paid` oldu.
- Redis kesintisinde Api ve Mvc ready 200 (`Degraded`) ve calismaya devam etti; kesinti sirasinda yeniden baslatilan Mvc replikasi key ring okunamadigi icin 500 verdi, Redis donunce 7 sn'de toparlandi.

## Olculmeyenler
- Tuketicilerin donmeme nedeni (hipotez: `RabbitMqConnectionProvider.GetConnectionAsync` kurtarilan baglantiyi dispose ediyor, tuketici dongusu eski baglantida kaliyor; dogrudan gozlenmedi).
- Giris yapmis Mvc oturumlarinin Redis kesintisinde ve anahtar kaybinda davranisi; nginx'in 500 donen replikaya yonlendirmesi.
- Uzlastirma esik degerleri ve DLQ'daki mesajlarin akibeti.

## Kisitlar
- Her kosul tek kez, yerel Docker Desktop'ta calistirildi.
- `docker stop` Docker DNS kaydini sildigi icin Postgres ve RabbitMQ arizalari DNS hatasi olarak gorundu; takili ya da baglantiyi reddeden sunucu taklit edilmedi.
