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