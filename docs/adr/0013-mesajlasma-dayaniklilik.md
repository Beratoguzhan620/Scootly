# ADR 0013: Mesajlaşma Dayanıklılığı — Retry ve Ölü Mektup Kuyruğu

## Durum
Kabul edildi.

## Bağlam
RideCompletedMessageConsumer, bir mesaj işlenirken hata alırsa sonsuza kadar
tekrar deneyebilirdi — bu, kalıcı olarak bozuk (örnek: geçersiz JSON, eksik
alan) bir mesajın kuyruğu sonsuza kadar tıkamasına yol açabilirdi.

## Karar
RabbitMQ'nun x-dead-letter-exchange kuyruk özelliği kullanıldı. Bir mesaj en
fazla 3 kez denenir (x-death başlığı üzerinden sayılır); 3. denemeden sonra
hâlâ başarısızsa, otomatik olarak ayrı bir ölü mektup kuyruğuna (DLQ) taşınır
ve ana kuyruk tıkanmaz.

## Sınırlama
Ölü mektup kuyruğundaki mesajları izleyen/uyaran bir mekanizma henüz yok —
şu an sadece RabbitMQ yönetim arayüzünden (http://localhost:15672) elle
gözlemlenebilir. İleride bir izleme/alarm servisi eklenebilir.
## Düzeltme (27.09.2026) — Önceki uygulama bu kararı gerçekleştirmiyordu

İlk uygulamada reddedilen mesaj doğrudan ölü mektup exchange'ine gidiyor ve oradan ana kuyruğa hiç dönmüyordu;
deneme sayısı her zaman 0 okunuyor, ilk hatada mesaj DLQ'ya düşüyordu. "Deneme hakkı bitti" dalı ise mesajı
DLQ'ya göndermek yerine `ack` ile siliyordu. Bu durum teknik incelemede tespit edildi.

Yeni topoloji (tüm kalıcı tüketiciler için `RabbitMqConsumerService` taban sınıfında):

```
scootly.events --(routing key)--> {kuyruk}
{kuyruk} --(nack, requeue:false)--> {kuyruk}.retry   (x-message-ttl = RetryDelayMilliseconds)
{kuyruk}.retry --(TTL doldu)--> {kuyruk}
{kuyruk} --(deneme hakkı bitti veya kalıcı hata)--> {kuyruk}.dlq   (önce onaylı yayın, sonra ack)
```

- Deneme sayısı, `x-death` başlığında ana kuyruğa ait "rejected" kaydının `count` alanından okunur
  (liste uzunluğundan değil; RabbitMQ aynı kuyruk/sebep çifti için tek kayıt tutar).
- Kalıcı hatalar (örn. ayrıştırılamayan içerik) yeniden denenmeden DLQ'ya taşınır.
- Varsayılanlar: 3 yeniden deneme, 10 sn bekleme (`Messaging:*` ayarları). Davranış, gerçek bir RabbitMQ
  container'ıyla `Scootly.Infrastructure.Tests` içinde doğrulanır.
- Kuyruk adları değişti (`scootly.payments.ride-charges`, `scootly.fieldops.battery-low`); eski
  `scootly.ride-completed-consumer*` kuyrukları artık kullanılmıyor ve silinebilir.

DLQ izleme/alarm mekanizması hâlâ yok (teknik borç).
