# ADR 0022: Mesajlaşma Güvenilirliği — Domain Olayları, Outbox ve Tüketiciler

## Durum
Kabul edildi (27.09.2026).

## Bağlam
Outbox deseni kurulmuştu ama uçtan uca güvence vermiyordu:

- Mesajlar kalıcı (persistent) işaretlenmiyor ve broker onayı beklenmeden "işlendi" sayılıyordu: broker yeniden
  başlarsa mesaj kaybolabiliyordu.
- Birden fazla API süreci aynı outbox satırlarını aynı anda yayınlayabiliyordu.
- Domain olayları üretiliyor ama hiçbir yere iletilmiyordu; bu yüzden örneğin `VehicleBatteryLow` hiç yayınlanmıyordu.
- Tüketiciler açılışta broker yoksa kalıcı olarak kapanıyor, retry/DLQ mantığı çalışmıyordu (bkz. ADR 0013 düzeltmesi).

## Karar

1. **Domain olayı → outbox (aynı transaction):** `ScootlyDbContext.SaveChangesAsync`, izlenen aggregate'lerin olaylarını
   `DomainEventOutboxMapper` ile entegrasyon olaylarına çevirip aynı transaction'da `OutboxMessages` tablosuna yazar ve
   olayları temizler. Böylece "veri kaydedildi ama olay kayboldu" ya da "olay yayınlandı ama veri geri alındı" durumu
   oluşmaz. Handler'lar outbox'a elle yazmaz; tek yayın yolu budur. Eşlenen olaylar: RideCompleted, RideAbandoned,
   VehicleBatteryLow, VehicleStatusChanged.
2. **Outbox yayıncısı (`OutboxProcessor`):** satırları `FOR UPDATE SKIP LOCKED` ile kilitler (çoklu süreç güvenli),
   mesajları kalıcı ve `MessageId` ile yayınlar, **publisher confirm** bekler; yayınlanamayan mesajda sırayı korumak için
   partiyi keser ve hatayı satıra yazar. API ve Worker'da çalışır. İşlenmiş kayıtlar 7 gün sonra silinir.
3. **Tüketiciler (`RabbitMqConsumerService`):** yeniden bağlanma, prefetch, gecikmeli retry kuyruğu ve DLQ (ADR 0013).
   Kalıcı kuyruklar rekabetçi tüketim içindir; canlı bildirimler için her API süreci kendi geçici kuyruğunu dinler
   (fan-out), böylece hangi instance'a bağlı olursa olsun her SignalR istemcisi bildirimi alır.
4. **Idempotency:** Tüketici işlemleri doğası gereği idempotent tasarlanır (ör. ödeme durumu kontrolü + sağlayıcı
   idempotency anahtarı). Ek olarak `ProcessedMessages` tablosu `(MessageId, Consumer)` anahtarıyla, aynı olayın aynı
   tüketicide tekrar uygulanmasını önler (webhook olayları).

## Bilinen sınırlama
Yönlendirilemeyen mesajlar (henüz hiçbir kuyruk bağlı değilken yayınlanan) broker tarafından düşürülür. Ödeme için
bu durum, Worker'daki uzlaştırma işiyle (ADR 0015) kapatılır; bildirimler için kabul edilebilir kayıptır.

## Alternatif: Hazır bir mesajlaşma kütüphanesi (MassTransit, NServiceBus)
Outbox, retry, DLQ ve idempotency'yi hazır sunar.

## Neden Seçilmedi
Projenin amacı bu mekanizmaları anlamak; ayrıca mevcut kapsam için gereken kod küçük ve gerçek RabbitMQ ile test ediliyor.
Tüketici ve olay türü sayısı arttığında bu karar yeniden değerlendirilmeli.
