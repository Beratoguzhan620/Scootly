# Scootly Domain Sözlüğü

Bu dosya, projede kullanılan her terimin tek, tutarlı bir tanımını içerir. Amaç: "ortak dil" (ubiquitous language) — bir geliştiricinin "rezervasyon" dediği şeyin, bir başkasının "rezervasyon" dediği şeyle her zaman aynı anlama gelmesi.

## Fleet (Filo) Context

- **Araç (Vehicle):** Sistemde kayıtlı, kullanıcılar tarafından kiralanabilir bir elektrikli scooter. Durum makinesi ile yönetilir: Available → Reserved → InRide → Available (ya da Maintenance).
- **Batarya Seviyesi (BatteryLevel):** Aracın şarj yüzdesi (0-100). Domain kuralları bu aralığı zorunlu kılar.
- **Araç Modeli (VehicleModel):** Aracın markası ve menzili (km).
- **Rezervasyon (Reservation — durum anlamında):** Bir aracın, henüz sürüş başlamadan önce, belirli bir sürücü için 10 dakika boyunca ayrılmış olması. `Vehicle.Status = Reserved` ile temsil edilir. Not: Bu terim, ayrı bir `Reservation` sınıfının varlığından bağımsız olarak, `Vehicle`'ın bir durumunu ifade eder (bkz. aşağıdaki "Aggregate Sınırı Notu").

## Riding (Sürüş) Context

- **Sürüş (Ride):** Bir sürücünün, rezerve ettiği aracı fiilen kullanmaya başlamasından (StartRide) bitirmesine (Complete) kadar geçen süreç. Kendi yaşam döngüsü vardır: Active → Completed / Abandoned / PaymentPending.
- **Terk Edilmiş Sürüş (Abandoned Ride):** Belirli bir süre (2 saat) boyunca `Active` durumda kalıp hiç tamamlanmayan sürüş; `AbandonedRideDetector` tarafından otomatik tespit edilir.
- **Ücret (Fare):** Bir sürüşün süresine göre hesaplanan tutar (dakika × birim fiyat).

## Telemetry (Telemetri) Context

- **Telemetri Okuması (TelemetryReading):** Bir aracın belirli bir andaki konum ve batarya bilgisini taşıyan tekil kayıt. Cihazlar tarafından periyodik olarak gönderilir.

## Ödeme (Payment — henüz ayrı bir context değil, Riding içinde gömülü)

- **Ödeme Yetkilendirmesi (Payment Authorization):** Bir sürüşün ücretinin, dış bir ödeme sağlayıcısından (bu projede simüle edilmiş) onaylanması süreci.
- **Ödeme Bekliyor (PaymentPending):** Ödeme yetkilendirmesi başarısız olduğunda sürüşün aldığı durum — araç yine de serbest bırakılır, ücret bir borç olarak kaydedilir.

## Coğrafya (Geo)

- **Coğrafi Nokta (GeoPoint):** Enlem/boylam çifti.
- **Hizmet Alanı (ServiceArea):** Aracın kiralanabileceği coğrafi sınır (henüz aktif olarak kullanılmıyor, tanımlı ama bağlanmamış).
- **Park Yasağı Bölgesi (NoParkingZone):** Aracın bırakılamayacağı coğrafi alan (henüz aktif olarak kullanılmıyor).

## Aggregate Sınırı Notu (79. gün incelemesi)

`Vehicle` aggregate'i şu an hem **filo bilgisini** (marka, menzil, batarya) hem
**anlık operasyonel durumu** (Status, ReservedAt) tek bir aggregate'te taşıyor.
Bu, 79. günde gözden geçirildi — aşağıdaki ADR 0019'a bakınız.