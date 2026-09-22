# ADR 0009: Toplu Yazma Stratejisi

## Durum
Kabul edildi.

## Bağlam
34 ve 41. günlerde ölçülen performans testlerinde, EF Core'un varsayılan
SaveChangesAsync davranışıyla 10.000 kayıt eklemenin ~800-2600 ms sürdüğü
gözlemlendi. 44. günde, aynı işlemin PostgreSQL'in yerel COPY (BINARY format)
mekanizmasıyla yapılması denendi.

## Ölçüm Sonucu

| Yöntem | 5.000 kayıt için süre |
|---|---|
| SaveChangesAsync (tekil INSERT) | 2691 ms |
| COPY BINARY (Npgsql BeginBinaryImportAsync) | 60 ms |

**~45 kat hızlanma.**

## Karar
Büyük hacimli, tek seferlik veri yazma ihtiyaçları (örnek: telemetri toplu
işleme — 11. haftanın konusu, ileride yazılacak) için EF Core'un SaveChangesAsync'i
DEĞİL, Npgsql'in NpgsqlBinaryImporter'ı (COPY BINARY) kullanılacak.

Normal kullanıcı işlemleri (tek bir Vehicle veya Ride ekleme, örnek: Register,
StartRide) için SaveChangesAsync kullanılmaya devam edilecek — çünkü bu işlemler
zaten tek satırlık, toplu yazmanın avantajı burada anlamsız, ve SaveChangesAsync'in
sağladığı change tracking, domain event yayınlama gibi avantajlar bu senaryoda
daha değerli.

## Alternatif: EFCore.BulkExtensions kütüphanesi
Üçüncü parti bir NuGet paketi ile toplu ekleme/güncelleme.

## Neden Seçilmedi
Npgsql'in kendi COPY mekanizması, PostgreSQL'e özgü en hızlı yol olduğu için
ve ekstra bir paket bağımlılığı gerektirmediği için tercih edildi. Ancak
BulkExtensions, ileride toplu UPDATE/DELETE ihtiyacı doğarsa (COPY yalnızca
INSERT için uygundur) değerlendirilebilir.

## Sınırlama
COPY BINARY yaklaşımı, domain nesnelerinin kurucularını ve iş kurallarını
(örnek: BatteryLevel'in 0-100 kontrolü) BYPASS EDER — veriler doğrudan
veritabanına yazılır, Domain katmanının koruması devreye girmez. Bu yöntem
yalnızca zaten doğrulanmış, güvenilir kaynaklardan gelen toplu veri için
kullanılmalı (örnek: bir migrasyon script'i, güvenilir bir telemetri kaynağı),
kullanıcı girdisi için asla kullanılmamalı.