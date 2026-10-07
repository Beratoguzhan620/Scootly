# ADR 0045: Hizmet bölgesi sınırı tek bir jsonb dizisinde saklanır

- Durum: Kabul edildi
- Tarih: 2026-10-07

## Bağlam

Hizmet bölgesi sınırı (`ServiceArea.Boundary`, en fazla 500 köşe) EF Core `OwnsMany` ile ayrı bir tabloda
(`ServiceAreaBoundaryPoints`) tutuluyordu. Poligonun köşe sırası anlamlıdır: bölge çözümleme (ray casting) ve
haritadaki çizim sıraya bağlıdır.

7 Ekim incelemesinde EF'nin ürettiği SQL'in alt kayıtları yalnızca bölge kimliğine göre sıraladığı, noktaların kendi
sırasını garanti eden bir `ORDER BY` içermediği görüldü (EF Core 6'dan beri son koleksiyonun anahtarı sıralamaya
eklenmez). Geçici bir Postgres 16.15'te ölçüldü:

| Veri | Çözümleyici sorgusu (`ORDER BY Id`) | Liste ucu (`ORDER BY Name, Id`) |
|---|---|---|
| 3 bölge × 50 nokta | 2 sıra bozulması | 0 |
| 10 bölge × 100 nokta | 590 | 397 |
| 20 bölge × 500 nokta | 0 (merge join) | 2998 |

Sonuç plana bağlıdır, yani doğru çalışması tesadüftür. Bozulduğunda araç yanlış bölgeye düşer, canlı bildirim yanlış
SignalR grubuna gider ve `GET /service-areas` karışık poligon döner.

## Seçenekler

1. Noktalara bir sıra sütunu eklemek. EF bir sahip koleksiyonunu belirli bir sütuna göre sıralayarak yükleyemez;
   sıralama her sorguda elle yazılmak ya da yükleme sonrası düzeltilmek zorunda kalır.
2. PostGIS `geometry(Polygon)`. Doğru araç, fakat yeni bir eklenti, imaj ve bağımlılık getirir; mevcut ihtiyaç
   (nokta poligon içinde mi?) bunu gerektirmiyor.
3. Sınırı tek bir `jsonb` dizisinde tutmak (`OwnsMany(...).ToJson("Boundary")`). Dizi sırası tanım gereği korunur.

## Karar

Seçenek 3. `ServiceAreas.Boundary` sütunu `jsonb` dizisidir (`[{ "Latitude": .., "Longitude": .. }, ...]`).
Migration `20261007123450_StoreServiceAreaBoundaryAsJson`:

- Sütunu ekler, mevcut noktaları ekleniş sırasıyla (`ORDER BY "Id"`) diziye taşır, eski tabloyu siler.
- `Down` tabloyu yeniden kurar ve noktaları dizi sırasıyla (`WITH ORDINALITY`) geri yazar.

## Ölçülenler

- Geçici Postgres'te ileri ve geri uygulandı: 2 bölge × 60 noktanın sırası her iki yönde korundu, eski tablo silindi.
- Testler: Api'de 4 bölge × 120 nokta yazılıp okunuyor; Infrastructure'da 5 bölge × 150 nokta ve içbükey (C biçimli)
  bir poligonda bölge çözümleme doğrulanıyor. Köşe sırası bozulursa C'nin boşluğundaki nokta yanlışlıkla içeride sayılır.
- Geliştirme yığınında migrator iki yeni migration'ı uyguladı, uygulama sağlıklı açıldı.

## Sonuçlar

- Bölge sınırı tek satırda okunur; join yoktur.
- Sınıra göre SQL'de mekânsal sorgu yapılamaz (yapılmıyordu da). İhtiyaç doğarsa PostGIS'e geçiş ayrı bir karardır.
