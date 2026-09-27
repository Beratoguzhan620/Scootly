# ADR 0019: Ride Aggregate Sınırı İncelemesi

## Durum
İncelendi, şimdilik değişiklik yapılmadı (bilinçli erteleme).

## Bağlam
79. günde Ride aggregate'i, DDD prensipleri açısından gözden geçirildi.

## Bulgular

**Ride aggregate'i genel olarak doğru boyutta:** DriverId, VehicleId,
StartLocation, EndLocation, StartedAt, EndedAt, Status alanları hepsi
"bir sürüşün her zaman tutarlı olması gereken çekirdek bilgisi" — bunları
ayırmak, atomik güncellemeyi (örnek: Complete() çağrısında EndLocation +
EndedAt + Status'un birlikte değişmesi) parçalayıp yeni bir tutarlılık
sorunu yaratırdı.

**Sorunlu nokta — Fare alanı:** Fare (ücret) ve MarkFarePaid/MarkPaymentPending
metotları, aslında Billing (Faturalama) context'ine ait bir kavram. Şu an
Ride'ın içine gömülü çünkü ayrı bir Billing context'i/aggregate'i henüz
yazılmadı (bkz. ADR 0017).

## Karar
Fare alanı ŞİMDİLİK Ride içinde bırakıldı — ayırmak, henüz var olmayan bir
Billing context'i gerektirir ve bu noktada spekülatif bir genişleme olurdu
(YAGNI ilkesi: henüz ihtiyaç duyulmayan bir soyutlamayı önceden kurmak).

## Gelecek Adım
Eğer/ne zaman gerçek bir Billing context'i yazılırsa (fatura geçmişi,
vergi hesaplama, farklı ödeme yöntemleri gibi gerçek karmaşıklık ortaya
çıkarsa), Fare ve ilgili metotlar Ride'dan çıkarılıp ayrı bir Invoice
aggregate'ine taşınmalı — Ride, yalnızca RideId + Fare (referans) tutar,
gerçek fatura detayları Billing context'inde yaşar.

## Ders
Aggregate sınırları statik değildir — bir alanın "doğru yerde" olup
olmadığı, o alanın etrafında gerçek iş karmaşıklığı (kurallar, geçmiş,
farklı durumlar) birikmeye başladığında yeniden değerlendirilmelidir.
Erken ayırmak (henüz karmaşıklık yokken), gereksiz dolaylılık yaratır.