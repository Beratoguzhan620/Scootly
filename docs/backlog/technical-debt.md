# Teknik Borç Listesi

Bu dosya, bilinçli olarak şimdi düzeltilmeyen ama fark edilen eksiklikleri kaydeder.

## Faz 1 sonu itibariyle

- `CancelReservationCommand` yazıldı ama handler'ı yok (7. gün — dokümanda bilinçli olarak bırakılmıştı, ileride tamamlanacak).
- `CompleteRideCommand`/`Complete` ucu için doğrulayıcı (validator) yok — `StartRideRequestValidator` gibi bir `CompleteRideRequestValidator` eklenebilir.
- `Wallet` ve `Tariff` domain tipleri henüz yazılmadı (Pricing/Billing context'leri ileriki günlerde gelecek).
- Migration dosyaları `Scootly.Infrastructure` projesinin kökünde duruyor, `Persistence/Migrations` altında değil (EF Core'un varsayılan davranışı, kozmetik bir fark, işlevsel sorun yok).