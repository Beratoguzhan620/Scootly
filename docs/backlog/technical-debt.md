# Teknik Borç Listesi

Bu dosya, bilinçli olarak şimdi düzeltilmeyen ama fark edilen eksiklikleri kaydeder.

## Faz 1 sonu itibariyle

- `CancelReservationCommand` yazıldı ama handler'ı yok (7. gün — dokümanda bilinçli olarak bırakılmıştı, ileride tamamlanacak).
- `CompleteRideCommand`/`Complete` ucu için doğrulayıcı (validator) yok — `StartRideRequestValidator` gibi bir `CompleteRideRequestValidator` eklenebilir.
- `Wallet` ve `Tariff` domain tipleri henüz yazılmadı (Pricing/Billing context'leri ileriki günlerde gelecek).
- Migration dosyaları `Scootly.Infrastructure` projesinin kökünde duruyor, `Persistence/Migrations` altında değil (EF Core'un varsayılan davranışı, kozmetik bir fark, işlevsel sorun yok).
- Kapsam raporu (18. gün): Genel çizgi kapsamı %61, kritik domain sınıfları (Vehicle %88, Ride %75, GeoPoint %78, GeofenceEvaluator %87) hedefin (%80) civarında veya üzerinde. Result<T>, ExceptionHandlingMiddleware ve henüz kullanılmayan tipler (Reservation, NoParkingZone, olay sınıfları) düşük kapsamlı — bunlar ilgili özellikler yazıldığında doğal olarak artacak.
	- `VehiclesController.Register` metodu `SaveChangesAsync` çağırmıyor — araç nesnesi oluşturuluyor ve `AddVehicle` ile izlemeye alınıyor ama veritabanına gerçekten kaydedilmiyor. Bu, dokümanın 23. gün planında sade tutulmuş basit bir örnekti; gerçek bir kullanım için `IUnitOfWork.SaveChangesAsync()` çağrısı eklenmeli.
	- **24. gün derste:** `Ride` sınıfındaki `DriverId`, `VehicleId`, `StartedAt` alanları yalnızca `get;` (set olmadan) tanımlıydı ve EF Core bunları otomatik olarak sütun sanmadı, ilk migration'a hiç girmediler — bu, veritabanı sorgularında sessizce eksik veri üretebilecek bir hataydı, ancak elle test ederken fark edildi. Çözüm: `RideConfiguration.cs`'e bu üç alan için açık `Property(...)` tanımları eklendi ve bir düzeltme migration'ı (`FixMissingRideColumns`) üretildi. **Ders:** yalnızca `get;` olan alanların gerçekten veritabanına yazıldığını, migration dosyasını göz atarak doğrulamak iyi bir alışkanlık.