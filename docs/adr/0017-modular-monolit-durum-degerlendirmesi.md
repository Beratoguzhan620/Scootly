# ADR 0017: Modüler Monolit — 76. Gün Durum Değerlendirmesi

## Durum
Gözden geçirildi, kasıtlı bir değişiklik yapılmadı.

## Bağlam
Dokümanın planladığı altı context (Riding, Fleet, Telemetry, Pricing, Billing,
FieldOps) arasında doğrudan çapraz çağrı olup olmadığı incelendi.

## Bulgular
- Riding, Fleet, Telemetry context'leri birbirinden bağımsız — hiçbiri
  diğerinin handler'ını doğrudan çağırmıyor.
- Pricing ve Billing context'leri henüz AYRI OLARAK YAZILMADI — ücret
  hesaplama mantığı (PerMinuteRate sabiti, fare hesaplaması),
  RideCompletedMessageConsumer içinde gömülü duruyor. FieldOps context'i de
  yok (BatteryLowConsumer, gerçek bir FieldTask oluşturmuyor, sadece log
  yazıyor — 64. günden beri bilinen bir basitleştirme).
- RideCompletedMessageConsumer, saga akışı gereği hem Ride hem Vehicle'a
  doğrudan dokunuyor — bu, context ihlali değil, aggregate'ler arası
  KOORDİNASYONUN doğal bir parçası (saga'nın tanımı gereği).

## Karar
Bugün context'leri zorla ayırmıyoruz çünkü (a) Pricing/Billing/FieldOps henüz
gerçek domain mantığı taşımıyor, ayırmak için erken; (b) mevcut context'ler
(Riding, Fleet, Telemetry) zaten temiz. Bunun yerine 77. günde yazılacak
mimari kural testi, gelecekte bu context'ler büyüdüğünde (örnek: Pricing
context'i gerçekten yazıldığında) aralarında yanlışlıkla doğrudan bağımlılık
oluşmasını otomatik olarak yakalayacak.

## Teknik Borç
Ücret hesaplama mantığının (PerMinuteRate ve ilgili hesaplama), kendi
Pricing context'ine (ayrı bir handler/servis olarak) çıkarılması, dokümanın
ileriki haftalarında (Pricing/Billing context'i gerçek olarak yazıldığında)
yapılmalı.