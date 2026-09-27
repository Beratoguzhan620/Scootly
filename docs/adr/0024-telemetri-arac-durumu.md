# ADR 0024: Telemetrinin Araç Durumunu Güncellemesi

## Durum
Kabul edildi (27.09.2026).

## Bağlam
Telemetri yalnızca ham okuma olarak saklanıyordu; aracın konumu ve bataryası hiç güncellenmediği için harita ve
`BatteryThresholdScanner` eski veriyle çalışıyor, `VehicleBatteryLowIntegrationEvent` hiç yayınlanmıyordu. Ayrıca
herhangi bir kullanıcı, var olmayan araçlar için bile telemetri gönderebiliyordu.

## Karar
- Telemetri ucu yalnızca cihaz token'larını kabul eder; parti en fazla 500 okuma olabilir, tüm değerler (konum, batarya,
  ölçüm zamanı: en fazla 5 dk ileri, 24 saat geri) kuyruğa alınmadan önce doğrulanır. Kayıtlı olmayan araçların okumaları
  reddedilir ve yanıtta listelenir.
- Tüketici partiyi tek transaction'da işler: ham okumaları saklar ve her araç için `Vehicle.ReportTelemetry` çağırır.
  Domain metodu sıra dışı (daha eski) okumaları yok sayar ve batarya %20'nin altına **ilk kez** indiğinde
  `VehicleBatteryLowEvent` üretir; olay outbox üzerinden Worker'daki `BatteryLowConsumer`'a ulaşır.
- Araç satırı xmin eşzamanlılık belirteciyle korunduğu için telemetri güncellemesi ile kullanıcı işlemi (ör. rezervasyon)
  nadiren çakışabilir. Her iki taraf da `OptimisticConcurrency` ile taze veriyle yeniden dener; çakışma kullanıcıya
  yansımaz.

## Alternatif: Konum/bataryayı ayrı bir tabloda tutmak
Aracın operasyonel durumunu (rezervasyon, sürüş) yüksek frekanslı telemetriden ayırarak çakışmayı tamamen önler.

## Neden Şimdilik Seçilmedi
Mevcut ölçekte (araç başına ~5 sn'de bir okuma) çakışma olasılığı çok düşük ve yeniden deneme ile şeffaf biçimde
çözülüyor; ayrı tablo, okuma sorgularına birleştirme (join) ve ikinci bir tutarlılık kaynağı ekler. Filo büyüdüğünde veya
telemetri frekansı arttığında yeniden değerlendirilmeli.
