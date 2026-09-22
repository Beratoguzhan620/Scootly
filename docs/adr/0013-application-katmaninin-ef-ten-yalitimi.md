# ADR 0013 — Sorgu handler'ları EF Core'a bağlanmadan asenkron çalışsın

- **Durum:** Kabul edildi
- **Gün:** 41–42
- **Bağlam:** Faz 3, Hafta 9

## Karar

`IQueryExecutor` adında bir Application arayüzü tanımlandı. Sorgu handler'ları
`ToListAsync` / `CountAsync` / `FirstOrDefaultAsync` çağrılarını bu arayüz
üzerinden yapıyor; EF Core uygulaması (`EfQueryExecutor`) Infrastructure'da.

## Sorun

Faz 3 ile birlikte sorgular controller'dan Application katmanına taşındı.
Taşınınca şu ortaya çıktı: `IQueryable<T>` bir LINQ tipi ve Application zaten
onu kullanabiliyor — ama `ToListAsync()` LINQ'e ait DEĞİL,
`Microsoft.EntityFrameworkCore` paketinin bir uzantı metodu.

Yani sorgu handler'larının Application'da yaşayabilmesi için ya EF Core paketi
Application'a girecekti, ya da başka bir yol bulunacaktı.

## Neden önemli

Bu, Karar 1'in ("Application somut teknolojiden bağımsız olmalı) en sinsi
ihlali olurdu. Paket referansı tek bir `using` satırıyla girer, kod incelemesinde
kimse fark etmez, ve o noktadan sonra Application içinde EF tiplerinin
görünmesi normalleşir. 38. günde `DbUpdateConcurrencyException` için aynı sınırı
korumuştuk; ekip arkadaşımızın dalında aynı sınır korunamadı ve kendi teknik
borç listesine "Karar 1'e küçük bir istisna" olarak yazıldı — o istisnalar
küçük başlıyor.

## Değerlendirilen seçenekler

### 1. Application'a EF Core eklemek

En kısa yol. Reddedildi: yukarıdaki gerekçe.

### 2. Sorgu handler'larını Infrastructure'a koymak

Katman ihlali yok, ama sorgu mantığı (hangi filtre, hangi sıralama, hangi
sayfa) bir iş kararıdır ve Infrastructure'da yaşaması uzun vadede
Application'ı boşaltır.

### 3. Yürütmeyi bir arayüzün arkasına almak

Seçilen. `IQueryable` ifadesi Application'da kuruluyor — yani ne sorulacağı
orada — ama çalıştırma Infrastructure'da.

## Sonuçlar

**Olumlu**

- Application'ın paket listesi Domain'inki kadar sade kaldı: hiç paket yok.
- Sorgu handler'ları EF olmadan test edilebilir: bellekteki bir liste
  `AsQueryable()` ile verilip sahte bir yürütücüyle çalıştırılabiliyor.

**Olumsuz**

- Fazladan bir arayüz ve her sorguda bir dolaylılık katmanı.
- Arayüz `IQueryable` alıyor, yani Application yine de LINQ sağlayıcısının
  davranışına bağımlı: çevrilemeyen bir ifade yazılırsa hata yine çalışma
  zamanında çıkar. Bu arayüz o sorunu çözmüyor, yalnızca paket bağımlılığını
  çözüyor. Dürüst olmak gerekirse tam bir yalıtım değil.

**Sessiz tuzak**

EF'in asenkron uzantıları yalnızca EF'in kendi sorgu sağlayıcısıyla çalışıyor.
Bellekteki bir listeye uygulanırsa çalışma zamanında `InvalidOperationException`
fırlatıyor — derleme temiz, hata testte. Sahte bir yürütücü kullanabilmek bu
yüzden ayrıca değerli.
