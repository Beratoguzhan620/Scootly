# ADR 0011: Worker Servislerinde Hata Toleransı

## Durum
Kabul edildi.

## Bağlam
.NET'te bir BackgroundService.ExecuteAsync içinde yakalanmayan bir istisna,
varsayılan olarak tüm host'u durdurur (IHostApplicationLifetime.StopApplication
tetiklenir). Scootly.Worker'daki üç servis (ReservationTimeoutService,
BatteryThresholdScanner, AbandonedRideDetector) başlangıçta bu korumaya sahip
değildi — veritabanına anlık bir erişim sorunu bile tüm worker'ı durdurabilirdi.

## Karar
Her servisin ana döngüsü bir try/catch ile sarıldı. Cancellation (uygulamanın
normal kapanışı) sessizce, hata olarak loglanmadan ele alınıyor. Diğer tüm
istisnalar loglanıp yutuluyor — döngü bir sonraki periyodik turda devam ediyor.
Bu, "bir tur başarısız olursa sistemin geri kalanı çalışmaya devam etsin" ilkesini
üç servise de eşit şekilde uyguluyor.

## Alternatif: Polly ile yeniden deneme politikası
Her veritabanı çağrısını Polly retry/circuit-breaker politikasıyla sarmak.

## Neden Seçilmedi (Şimdilik)
Bu servisler zaten periyodik olarak (30 saniye - 10 dakika aralıklarla) tekrar
çalışıyor — bir turun başarısız olup bir sonraki turda kendiliğinden düzelmesi,
ayrı bir retry mekanizmasına göre yeterli. Polly, 13-14. haftada (dış servis
çağrıları, ödeme entegrasyonu gibi daha kritik senaryolarda) değerlendirilecek.

## Güncelleme (69-70. gün)

Aynı problem, Scootly.Worker'daki servislerin dışında, Scootly.Api'nin kendi
hosted service'inde (RideCompletedMessageConsumer) de tekrarlandı: ExecuteAsync'in
BAŞLANGIÇ kısmı (bağlantı kurma, exchange/kuyruk tanımlama) try/catch dışında
bırakılmıştı. Test ortamında RabbitMQ container'ı olmadığında, bu başlangıç
kodu istisna fırlatıp TÜM test host'unu (WebApplicationFactory tabanlı tüm
entegrasyon testlerini) çökertti — 17 test "ObjectDisposedException" ile
başarısız oldu.

**Genişletilmiş kural:** Bir BackgroundService'in try/catch koruması yalnızca
döngü içindeki periyodik işi değil, ExecuteAsync'in TÜM gövdesini (bağlantı
kurma dahil, döngü dışındaki hazırlık kodu dahil) kapsamalıdır.