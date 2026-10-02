# ADR 0026: Arayüz Yetkilendirmesi ve Hata Sayfaları (87. Gün)

## Durum
Kabul edildi.

## Yapılanlar
1. **Politika konumu doğrulandı:** `PolicyNames`, `RideOwnerRequirement`, `RideOwnerHandler`,
   `AddScootlyAuthorization()` — hepsi zaten `Scootly.Infrastructure`'da (84. günde
   taşınmıştı, bkz. commit geçmişi). Api ve Mvc aynı tanımı paylaşıyor.
2. **Rol bazlı menü:** `_Navigation.cshtml`, `User.IsInRole(...)` ile "Görevler"
   linkini FieldOperator/FleetManager'a, "Sürüşlerim" linkini Driver'a göre
   koşullu gösteriyor. (Ayrıca önceki bir HTML iç içe geçme hatası düzeltildi.)
3. **Hata sayfaları:** `Error.cshtml` Türkçeleştirildi, geliştirme ortamı uyarısı
   kaldırıldı (teknik detay sızdırmama ilkesi), durum koduna göre (403/404/genel)
   farklı başlık gösteriyor. `UseStatusCodePagesWithReExecute` eklendi.
4. **Gerçek doğrulama:** `surucu-b@scootly.com` (yalnızca Driver rolü) ile
   `/FieldTasks`'a URL ile doğrudan girildiğinde `/Account/AccessDenied`'a
   yönlendirildiği ve "Erişim Reddedildi" mesajının göründüğü tarayıcıda
   kanıtlandı.

## Kapsam Dışı (teknik borç)
Mvc tarafı için otomatik 403/AccessDenied entegrasyon testi yazılmadı —
mevcut `ScootlyApiFactory`, Api'nin JWT akışına özel. Davranış elle doğrulandı.