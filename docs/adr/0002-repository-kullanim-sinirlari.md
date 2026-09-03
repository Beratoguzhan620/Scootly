# ADR 0002: Repository Kullanım Sınırları

## Durum
Kabul edildi.

## Bağlam
EF Core'un DbContext'i zaten Unit of Work ve genel amaçlı bir repository gibi çalışıyor.
Her şeyi tekrar bir repository katmanının arkasına almak, DbContext'in sağladığı projeksiyon
ve no-tracking gibi performans araçlarını gizleyebilir.

## Karar
Repository sınıfları (VehicleRepository, RideRepository) yalnızca aggregate bütünlüğü
gerektiren yazma işlemleri için kullanılır: kimliğe göre getirme ve ekleme. Liste, rapor
ve filtreleme amaçlı okuma sorguları repository'ye konulmaz; bunlar Application katmanında
doğrudan IApplicationDbContext üzerinden, projeksiyonla yazılır (örnek: FindNearbyVehiclesQuery).

## Alternatif: Her sorgu için repository metodu yazmak
Örnek: IVehicleRepository.GetNearby(GeoPoint, double radius) gibi.

## Neden Seçilmedi
Bu yaklaşım, her yeni okuma ihtiyacında repository arayüzünü şişirir ve DbContext'in
sunduğu esnek LINQ sorgularını (projeksiyon, sayfalama, filtreleme) bir metot ardına
gizler. 9. haftada performans iyileştirmesi yaparken bu esnekliğe ihtiyaç duyacağız.