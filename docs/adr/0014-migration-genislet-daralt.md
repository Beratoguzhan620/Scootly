# ADR 0014 — Riskli şema değişiklikleri genişlet-daralt ile

- **Durum:** Kabul edildi (uygulanacak yöntem olarak; henüz uygulanmadı)
- **Gün:** 45
- **Bağlam:** Faz 3, Hafta 9

## Karar

Üretimde çalışan bir sistemde bir sütunu yeniden adlandırmak, tipini
değiştirmek veya kaldırmak **tek adımda yapılmaz**. Üç aşamaya bölünür:
genişlet → taşı → daralt.

## Sorun

`dotnet ef migrations add RenameColumn` tek bir `ALTER TABLE ... RENAME
COLUMN` üretir. Bu migration çalıştığı anda:

- Eski kod hâlâ ayakta ve eski sütun adını arıyor → sorguları patlıyor.
- Yeni kod henüz dağıtılmadıysa yeni adı kimse kullanmıyor.

Yani migration ile dağıtım arasındaki pencere boyunca sistem **kesintiye**
giriyor. Tek kopyalı bir sistemde bu pencere saniyeler, ama yatay
ölçeklenmiş bir sistemde (20. hafta) eski ve yeni kod **aynı anda** çalışıyor
ve pencere hiç kapanmıyor.

## Üç adım

Örnek: `Vehicles.Brand` sütununu `ManufacturerName` yapmak.

### 1. Genişlet (expand)

Yeni sütun **eklenir**, eskisi durur. Yeni sütun nullable — mevcut satırlarda
değeri yok.

```
migrationBuilder.AddColumn<string>("ManufacturerName", "Vehicles", nullable: true);
```

Bu migration tek başına güvenli: eski kod yeni sütunu görmüyor bile.

### 2. Taşı (migrate)

Uygulama **her iki sütuna da yazacak** şekilde güncellenir, okumayı hâlâ
eskisinden yapar. Bu sürüm dağıtıldıktan sonra mevcut satırlar tek seferlik
bir betikle doldurulur:

```
UPDATE "Vehicles" SET "ManufacturerName" = "Brand" WHERE "ManufacturerName" IS NULL;
```

Sonra okuma yeni sütuna çevrilir ve bu sürüm de dağıtılır. Bu noktada eski
sütuna kimse dokunmuyor ama duruyor — **geri dönüş yolu açık.**

### 3. Daralt (contract)

Yeni sütun `NOT NULL` yapılır ve eski sütun düşürülür.

```
migrationBuilder.DropColumn("Brand", "Vehicles");
```

Bu adım, önceki sürümün geri alınamayacağı noktadır. Bu yüzden 2. adım ile
3. adım arasında **en az bir sürüm** beklenir.

## Neden bu proje için önemli

Faz 2'de bunun hafif bir hali zaten yaşandı: `Ride.StartedAt` alanı hiçbir
migration'a girmemişti ve sonradan eklendi. O ekleme tesadüfen güvenliydi
(nullable olmayan ama varsayılan değerli bir sütun ekleniyordu ve kimse
okumuyordu). Kural olmadan güvenli olmak, bir sonraki sefer güvenli olmayı
garanti etmiyor.

## Sonuçlar

**Olumlu**

- Hiçbir noktada eski kod yeni şemayla veya yeni kod eski şemayla karşılaşmıyor.
- Her adım geri alınabilir (3. adıma kadar).

**Olumsuz**

- Tek bir değişiklik üç dağıtıma yayılıyor. Küçük bir ekipte bu, hafta süren
  bir iş demek ve yarıda bırakılma riski taşıyor — "2. adımda kalmış, iki
  sütunu da yazan" bir kod tabanı, tek sütunlu halinden kötü.
- Ara dönemde veri iki yerde: ikisi arasında tutarsızlık oluşursa hangisinin
  doğru olduğu belirsiz.

Bu yüzden yöntem **yalnızca üretimde veri olan tablolar için** zorunlu. Faz 3
sonu itibarıyla o durumda olan tablo yok; kural ileriye dönük.
