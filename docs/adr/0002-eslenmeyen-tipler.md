\# 0002 — Tariff, Wallet ve ServiceArea eşlemelerinin ertelenmesi



Tarih: 2026-08-29

Durum: Kabul edildi



\## Bağlam



Gün 12 planı, `ServiceAreaConfiguration`, `TariffConfiguration` ve

`WalletConfiguration` yazılmasını istiyor. Ancak `Tariff`, `Wallet` ve `Money`

tipleri domain'de tanımlı değil — planın hiçbir günü bu tipleri oluşturmuyor,

yalnızca hedef klasör yapısında yer alıyorlar.



`ServiceArea` mevcut ama kimliği yok; `Entity`'den türemediği için bir tabloya

eşlenemiyor.



\## Karar



İlk migration yalnızca `Vehicle` ve `Ride` aggregate'lerini kapsıyor.

Diğer üç eşleme, ilgili domain tipleri yazıldığında ayrı bir migration ile

eklenecek.



\## Değerlendirilen alternatifler



\*\*Tipleri şimdi uydurmak\*\* — `Tariff` ve `Wallet`'ı tahmine dayalı alanlarla

yazmak. Seçilmedi: iş kuralları henüz belirlenmemişken yazılan bir aggregate,

kural netleştiğinde yeniden yazılır ve arada bir migration borcu bırakır.



\*\*`ServiceArea`'yı Entity'ye çevirmek\*\* — kimlik ekleyip bugün eşlemek.

Seçilmedi: mevcut `GeofenceEvaluatorTests` testlerini kırıyor ve `ServiceArea`

şu an yalnızca hesaplamada kullanılıyor, kalıcılığa ihtiyacı yok.



\## Sonuçlar



\*\*Olumlu:\*\* İlk migration küçük ve doğrulanabilir. Şemada kullanılmayan tablo yok.



\*\*Olumsuz:\*\* Hedef klasör yapısıyla mevcut durum arasında bilinçli bir fark var.

Geofence bölgeleri kalıcı hale gelene kadar yalnızca kodda tanımlanabiliyor.

