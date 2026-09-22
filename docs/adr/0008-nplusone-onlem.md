# ADR 0008: N+1 Sorgu Problemi — Tespit ve Önlem

## Durum
Kabul edildi.

## Bağlam
Bir listedeki her öğe için ilişkili veriyi ayrı bir sorguyla çekmek (N+1 problemi),
EF Core'un en sık karşılaşılan performans hatasıdır. 42. günde, bir DbCommandInterceptor
kullanılarak bu somut olarak sayıldı: 10 Ride kaydı için, her birinin ilişkili Vehicle'ını
ayrı ayrı sorgulamak toplam 11 SQL sorgusu üretti (1 + N).

## Karar
İlişkili veriye ihtiyaç duyan tüm liste sorguları, döngü içinde ayrı sorgu atmak yerine,
tek bir LINQ ifadesinde (subquery, join veya EF Core'un Include mekanizması ile)
birleştirilecek. 42. günde ölçülen düzeltme, aynı 10 kayıt için sorgu sayısını 11'den
1'e indirdi.

## Tespit Yöntemi
appsettings.Development.json'da "Microsoft.EntityFrameworkCore.Database.Command": "Information"
log seviyesi açık tutulacak — geliştirme sırasında konsolda beklenmedik sayıda tekrar
eden sorgu görülürse, bu bir N+1 belirtisidir.

## Not
QueryCounter interceptor'ı yalnızca test amaçlı yazıldı (tests/Scootly.Concurrency.Tests
altında) — üretim kodunun bir parçası değil, kalıcı bir izleme aracı olarak düşünülmedi.