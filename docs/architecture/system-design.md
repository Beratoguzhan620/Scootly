# Sistem tasarimi

Durum: 108. gunde baslandi (bulut esleme). 110. gunde bounded context'ler, bir surusun yolculugu ve risk listesi eklenecek.

## 1. Bulut esleme tablosu

Bu tablo yon gosterir. Hicbir bulut hesabinda kurulum yapilmadi; maliyet, gecis suresi ve performans olculmedi.

| Bugun (compose) | Bulut karsiligi (kategori) | Tasimada dikkat edilecekler |
|---|---|---|
| PostgreSQL | Yonetilen PostgreSQL | Postgres'e ozgu ozellikler kullaniliyor (xmin eszamanlilik belirteci, kismi benzersiz indeks); Postgres uyumlu olmayan bir motora gecilmez. 106. gun: DB durunca api /health/ready 503 verdi (olculdu). |
| Redis | Yonetilen onbellek | Mvc Data Protection anahtar halkasi, oturum ve dagitik onbellek Redis'te; salt onbellek gibi dusunulmemeli. Redis durunca mevcut replikalar calisti (Degraded), yeni Mvc replikasi 500 verdi (106. gun olculdu). Anahtar halkasi kaybinin cerezlere etkisi olculmedi. |
| RabbitMQ | Yonetilen AMQP 0-9-1 kuyrugu (RabbitMQ uyumlu) | Topic exchange scootly.events, routing key = olay turu, kalici kuyruklar, retry/DLQ ve iki exclusive auto-delete kuyruk kullaniliyor. SQS/SNS gibi AMQP olmayan hizmetler dogrudan karsilik olmaz. 106. gun: baglanti kopmasindan sonra tuketicilerin geri gelmemesi gozlendi (kok neden hipotez, kod duzeltilmedi); tasimadan once yeniden olculmeli. |
| Nginx | Yuk dengeleyici (TLS sonlandirma) | Bugun tek giris 127.0.0.1:8443, api ve mvc icin ikiser replika, mavi-yesil gecis nginx reload ile (ADR 0041). Bulutta hedef grubu/agirlik gecisine donusur. Saglik kontrolu icin /health/live daha guvenli aday: /health/ready bagimlilik kesintisinde 503 verir (olculdu) ve LB hepsini havuzdan cikarabilir (LB davranisi olculmedi). |
| GHCR | Container registry | CI/is akisi dosyalarinda ghcr.io gecer; yayin adiminin gercekten calistigi bu belgede dogrulanmadi. |
| Seq / Jaeger / Prometheus / Grafana / OTel collector | Yonetilen gozlemlenebilirlik (OTLP kabul eden) | ADR 0030: uygulama OTLP ile collector'a yazar; arka uc degisimi esas olarak collector yapilandirmasidir. Prod yiginda observability profili yok, Prometheus alarm kurali yok (teknik borc, 107. gun). |
| api / mvc / worker konteynerleri | Konteyner calistirma (orkestrasyon) | Worker saglik kontrolu heartbeat dosyasi; worker bugun tek ornek. Replika/otomatik yeniden baslatma ayarlari olculmedi. |
| .env.prod dosyasi | Gizli yonetim servisi | 27.09.2026 olayi (postmortems/2026-09-27-sizmis-sirlar.md) ve rotasyon proseduru (incident-response.md bolum 6, denenmedi) bu gecisin gerekcesi. |
| SeaweedFS (S3 uyumlu, AWSSDK.S3) (108b'de eklendi (ADR 0043)) | Nesne depolama (S3 uyumlu) | Henuz yok. Kod S3 API ile yazilirsa gecis esas olarak yapilandirma olur. |

## 2. Olculmeyenler

- Hicbir hizmetin bulut karsiligi denenmedi; tablo bilgiye dayali esleme.
- Dev compose'daki Redpanda bu tabloya alinmadi (rolu bu belgede dogrulanmadi).
- Maliyet, gecis suresi, ag gecikmesi ve yonetilen hizmet sinirlari olculmedi.
