# Scootly Domain Sözlüğü

Bu dosya, projede kullanılan her terimin tek, tutarlı bir tanımını içerir. Amaç: "ortak dil" (ubiquitous language) — bir geliştiricinin "rezervasyon" dediği şeyin, bir başkasının "rezervasyon" dediği şeyle her zaman aynı anlama gelmesi.

## Fleet (Filo) Context

- **Araç (Vehicle):** Sistemde kayıtlı, kullanıcılar tarafından kiralanabilir bir elektrikli scooter. Durum makinesi ile yönetilir:
  - Available → Reserved → InRide → Available
  - Reserved → Available (sürücünün iptali veya süre dolumu)
  - InRide → Maintenance (terk edilmiş sürüş; saha kontrolü gerekir)
  - Available / Reserved / Lost → Maintenance → Available (bakım ve hizmete dönüş)
  - Available / Reserved / Maintenance → Lost → Available (bulunamayan araç kiralamadan çekilir, bulununca hizmete döner)
  - Sürüşteki bir araç bakıma alınamaz ve kayıp işaretlenemez.
- **Batarya Seviyesi (BatteryLevel):** Aracın şarj yüzdesi (0-100). %20'nin altı "düşük" sayılır; eşiğin ilk kez aşılması `VehicleBatteryLow` olayını üretir. %10'un altındaki araç kiralanamaz (`IsRentable`).
- **Araç Modeli (VehicleModel):** Aracın markası (en fazla 100 karakter) ve menzili (1-1000 km).
- **Rezervasyon (Reservation — durum anlamında):** Bir aracın, sürüş başlamadan önce **belirli bir sürücü** için 10 dakika boyunca ayrılmış olması. `Vehicle.Status = Reserved`, `Vehicle.ReservedBy` (sürücü) ve `Vehicle.ReservedAt` ile temsil edilir. Yalnızca rezervasyonu yapan sürücü o araçla sürüş başlatabilir veya rezervasyonu iptal edebilir. Bir sürücünün aynı anda yalnızca bir aktif rezervasyonu olabilir. Süre kuralı `ReservationPolicy` içindedir.
- **Son Bilinen Durum:** Aracın konumu ve bataryası, cihaz telemetrisiyle güncellenir (`LastTelemetryAt`); sıra dışı gelen eski okumalar yok sayılır. Sürüş bitişinde telemetri son 2 dakikada geldiyse cihazın konumu, istemcinin bildirdiği konuma tercih edilir.

## Riding (Sürüş) Context

- **Sürüş (Ride):** Bir sürücünün, rezerve ettiği aracı fiilen kullanmaya başlamasından (StartRide) bitirmesine (Complete) kadar geçen süreç. Yaşam döngüsü: Active → Completed / Abandoned. Bir sürücünün ve bir aracın aynı anda yalnızca bir aktif sürüşü olabilir.
- **Terk Edilmiş Sürüş (Abandoned Ride):** Eşik süreden (varsayılan 2 saat, yapılandırılabilir) uzun süre `Active` kalan sürüş. `AbandonedRideDetector` sürüşü kapatır, geçen süre kadar ücretlendirir ve aracı saha kontrolü için bakıma alır.
- **Tarife (Tariff):** Açılış ücreti + başlamış her dakika için dakika ücreti; en az 1 dakika ücretlendirilir (standart: 0 TL açılış, 2,50 TL/dakika).
- **Ücret (Fare):** Sürüş tamamlandığında veya terk edildiğinde tarifeyle hesaplanan tutar.

## Ödeme (Payment — henüz ayrı bir context değil, Riding içinde gömülü; bkz. ADR 0019, 0023)

- **Ödeme Durumu (PaymentStatus):** Sürüşün yaşam döngüsünden bağımsız ödeme durumu:
  - **None:** Sürüş devam ediyor, henüz ücret yok.
  - **Pending:** Ücret belirlendi; ödeme sağlayıcısından onay bekleniyor veya ret sonrası yeniden denenecek.
  - **Paid:** Ödeme alındı.
  - **Failed:** Azami deneme sayısına (5) ulaşıldı; tahsilat operasyon ekibine devredilir. Sağlayıcıdan geç gelen bir onay (webhook) yine de sürüşü ödenmiş sayar.
- **Ödeme Denemesi:** Sağlayıcının kalıcı reddi (örn. yetersiz bakiye) bir deneme sayılır; sağlayıcıya ulaşılamaması (geçici hata) deneme sayılmaz.
- **Idempotency Anahtarı:** Her ödeme denemesi `ride-{sürüş}-attempt-{n}` anahtarıyla gönderilir; aynı deneme tekrarlansa bile sağlayıcı ikinci kez tahsil etmez.

## Telemetry (Telemetri) Context

- **Telemetri Okuması (TelemetryReading):** Bir aracın belirli bir andaki konum ve batarya bilgisini taşıyan tekil kayıt. Yalnızca kayıtlı araçlar için kabul edilir, 30 gün saklanır.
- **Cihaz Ağ Geçidi (Device Gateway):** Birden fazla aracın telemetrisini gönderen, cihaz kimlik bilgileriyle doğrulanan istemci (bkz. ADR 0021).

## Coğrafya (Geo)

- **Coğrafi Nokta (GeoPoint):** Enlem/boylam çifti (sonlu ve geçerli aralıkta olmalı).
- **Hizmet Bölgesi (ServiceArea):** Adı benzersiz, en az 3 farklı noktadan oluşan poligon. Köşe sırası anlamlıdır ve tek bir jsonb dizisinde korunur (ADR 0045). Canlı filo bildirimleri, aracın bulunduğu hizmet bölgesinin grubuna gönderilir; hiçbir bölgeye düşmeyen konumlar `default-region` grubuna gider.
- **Park Yasağı Bölgesi:** Planlanan kavram; veri modeli ve iş kuralı henüz yok (kullanılmayan `NoParkingZone` tipi 7 Ekim 2026'da kaldırıldı).

## Aggregate Sınırı Notu (79. gün incelemesi)

`Vehicle` aggregate'i hem **filo bilgisini** (marka, menzil, batarya) hem
**anlık operasyonel durumu** (Status, ReservedBy, ReservedAt) tek bir aggregate'te taşıyor.
Bu, 79. günde gözden geçirildi — ADR 0019'a bakınız.
