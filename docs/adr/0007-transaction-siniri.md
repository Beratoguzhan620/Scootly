# ADR 0007: Transaction Sınırı Disiplini

## Durum
Kabul edildi.

## Bağlam
Her komut handler'ı (ReserveVehicleCommandHandler, StartRideCommandHandler,
CompleteRideCommandHandler), repository üzerinden okuma/yazma yapıp en sonunda
IUnitOfWork.SaveChangesAsync() çağırıyor. EF Core, bu işlemi otomatik olarak
tek bir veritabanı transaction'ı içinde yürütüyor.

## Karar
Transaction sınırı, her zaman tek bir handler'ın Handle metoduyla örtüşecek.
Hiçbir handler, SaveChangesAsync çağrısından önce bir dış servis (HTTP, e-posta,
ödeme sağlayıcısı) çağırmayacak — dış servis çağrıları ya SaveChangesAsync'ten
SONRA yapılacak, ya da (13-14. haftada göreceğimiz gibi) outbox deseniyle
asenkron bir akışa devredilecek.

Ayrı bir UnitOfWork sınıfı yazılmadı çünkü ScootlyDbContext zaten IUnitOfWork'ü
uyguluyor (bkz. ADR 0002) — EF Core'un DbContext'i doğası gereği bir Unit of Work'tür.

## Alternatif: Her handler'ı elle transaction bloğuna sarmak
`await using var transaction = await dbContext.Database.BeginTransactionAsync();`
şeklinde her handler'da açık transaction yönetimi yapmak.

## Neden Seçilmedi
EF Core'un SaveChangesAsync'i zaten otomatik olarak transaction açıp kapatıyor
(32. günde bunu izolasyon seviyesi deneylerinde elle açtığımızda gördük — o,
özel bir ölçüm senaryosuydu, normal handler akışında gerekli değil). Elle transaction
yönetimi, yalnızca birden fazla SaveChangesAsync çağrısının atomik olması gerektiğinde
(örnek: iki farklı DbContext işlemi) gerekli olur, ki şu an böyle bir senaryomuz yok.

## Teknik Borç
TransactionBehavior sınıfı yazıldı ama otomatik bir pipeline'a bağlı değil (MediatR
gibi bir kütüphane kullanmadığımız için) — şu an yalnızca elle çağrılabilir bir
gözlemleme/loglama katmanı. Gerçek bir pipeline ihtiyacı doğarsa (örnek: çok sayıda
handler'da tekrar eden çapraz kesit kod biriktiğinde) ileride eklenebilir.