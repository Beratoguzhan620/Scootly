# ADR 0012: Telemetri Kuyruğu — Aynı Process İçinde Tutma Kararı

## Durum
Kabul edildi (geçici — mesajlaşma altyapısı geldiğinde gözden geçirilecek).

## Bağlam
62. günde System.Threading.Channels ile bellek içi bir telemetri kuyruğu kuruldu.
İlk tasarımda, kuyruğu tüketen servisin Scootly.Worker (ayrı bir process) olması
planlanmıştı — ama bu YANLIŞ bir varsayımdı: bellek içi bir Channel, yalnızca
aynı .NET process'i içinde paylaşılabilir. Scootly.Api ve Scootly.Worker ayrı
process'ler olduğu için, API'de kuyruğa yazılan bir kayıt, Worker'ın kendi
(boş) kuyruğundan asla okunamazdı.

## Karar
TelemetryChannelConsumer, Scootly.Worker'a değil, Scootly.Api'nin kendi içine
bir BackgroundService (hosted service) olarak eklendi. Böylece hem yazan (Controller)
hem okuyan (Consumer) aynı process'te, aynı TelemetryChannel örneğini paylaşıyor.

## Sınırlama (Bilinçli)
Bu tasarım, API process'i yeniden başlatılırsa (deploy, çökme, vb.) kuyruktaki
işlenmemiş kayıtların kaybolacağı anlamına gelir — bellek içi bir yapı, kalıcı
değildir. Yüksek trafikte veya birden fazla API kopyası (yatay ölçekleme) varsa
bu yaklaşım yetersiz kalır.

## Gelecek Adım
13-14. haftada RabbitMQ/outbox deseni kurulduğunda, bu bellek içi Channel'ın yerini
gerçek bir mesaj kuyruğu (kalıcı, process'ler arası, yeniden başlatmaya dayanıklı)
alacak. Bugünkü Channel yaklaşımı, o zamana kadar geçerli bir "eğitim amaçlı basit
model" olarak bırakılıyor.
## Güncelleme (27.09.2026)

Kuyruk hâlâ süreç içi bir `Channel`, ancak davranışı değişti:

- **Sessiz veri kaybı yok:** `DropOldest` yerine geri basınç uygulanıyor. Parti ya tamamen kuyruğa alınır ya da hiç
  alınmaz; kuyruk doluysa cihaza `503 + Retry-After` döner ve cihaz aynı partiyi tekrar gönderir.
- **Toplu yazma:** Tüketici, 500 okuma veya 1 saniye (hangisi önce dolarsa) biriktirip tek transaction'da yazar
  (daha önce her okuma için ayrı `SaveChanges` çağrılıyordu).
- **Araç durumu güncellenir:** Aynı transaction'da aracın son bilinen konumu/bataryası güncellenir ve batarya eşiği
  aşılırsa olay outbox'a yazılır (bkz. ADR 0024).

Süreç yeniden başlatıldığında kuyrukta bekleyen okumaların kaybolabileceği sınırlaması geçerliliğini koruyor;
kapanışta kuyrukta kalanlar mümkün olduğunca kaydedilir.
