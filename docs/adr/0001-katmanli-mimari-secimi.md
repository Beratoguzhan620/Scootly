# ADR 0001: Katmanlı Mimari Seçimi (Clean Architecture)

## Durum
Kabul edildi.

## Bağlam
Proje, iş kurallarının (Domain), uygulama akışının (Application) ve teknik detayların
(Infrastructure, Api) birbirinden bağımsız gelişebilmesi gereken bir sistem olacak.

## Karar
Clean Architecture yaklaşımı seçildi: Domain hiçbir projeye bağımlı değil, Application
sadece Domain'e bağımlı, Infrastructure hem Domain'e hem Application'a bağımlı, Api ise
Application ve Infrastructure'a bağımlı. Bağımlılık oku her zaman içe (Domain'e) doğru.

## Alternatif: Vertical Slice Architecture
Her özelliğin (feature) kendi klasöründe, katmanlara bölünmeden tek parça yazılması.

## Neden Seçilmedi
Vertical Slice, yeni başlayan bir geliştirici için katman sorumluluklarını (hangi kod
nereye ait) net göstermiyor. Clean Architecture, "bu kod nereye yazılmalı" sorusuna
her zaman net bir cevap veriyor: iş kuralı Domain'e, akış Application'a, teknik detay
Infrastructure'a.