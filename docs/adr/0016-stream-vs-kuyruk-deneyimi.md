# ADR 0016: RabbitMQ (Kuyruk) vs Redpanda (Log Tabanlı Stream) — Deneyim

## Durum
Deneysel, projeye kalıcı olarak entegre edilmedi.

## Bağlam
75. günde, dokümanın "bu proje için gereksiz ama ne olduğunu görmek için"
notuyla Redpanda kuruldu ve RabbitMQ ile karşılaştırıldı.

## Karşılaştırma

| Özellik | RabbitMQ (Kuyruk) | Redpanda (Log/Stream) |
|---|---|---|
| Mesaj okunduktan sonra | Silinir (ack ile) | Kalır, tekrar okunabilir |
| Birden fazla bağımsız tüketici | Yalnızca biri alır (rekabetçi tüketim) | Her grup kendi hızında, bağımsız okur |
| Sıralama garantisi | Kuyruk bazında | Partition (key) bazında |
| Tipik kullanım | İş kuyruğu, komut işleme | Olay arşivi, analitik, yeniden oynatma |

## Ölçüm Sonucu
StreamReplayTests ile doğrulandı: iki farklı tüketici grubu (farklı groupId),
aynı mesajı ikisi de başarıyla okuyabildi (True, True, True) — bu, RabbitMQ'da
ikinci tüketicinin "boş kuyruk" görmesiyle tezat oluşturuyor.

## Karar
Redpanda, projenin gerçek mimarisine ENTEGRE EDİLMEDİ. TelemetryStreamProducer
ve TelemetryStreamConsumer, yalnızca bu deneyi yapmak için yazıldı, hiçbir
gerçek uç veya arka plan servisi bunları çağırmıyor.

## Dürüstlük Notu (dokümandan)
Redpanda bu proje için gereksizdir. Tek şehirlik bir filo için RabbitMQ
fazlasıyla yeterlidir. Bu deney, yalnızca "ne zaman gerçekten log tabanlı
bir stream'e ihtiyaç duyulur" sorusuna, soyut bir okuma yerine kurup
deneyerek cevap vermek içindi — örnek gerçek ihtiyaç senaryosu: birden
fazla bağımsız ekibin (analitik, faturalama, arşivleme) aynı telemetri
verisini kendi hızlarında, birbirinden habersiz işlemesi gerektiğinde.