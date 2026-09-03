# ADR 0003: Test Stratejisi — Unit ve Integration Ayrımı

## Durum
Kabul edildi.

## Bağlam
Domain kuralları, Application handler'ları ve gerçek HTTP/veritabanı akışı farklı
hızlarda ve farklı güven seviyelerinde test edilmesi gereken katmanlardır. Tek bir
test stratejisi (hepsi gerçek veritabanına karşı, ya da hepsi sahte nesnelerle)
ya çok yavaş ya da yeterince güvenilir olmayan bir test paketi üretir.

## Karar
Üç ayrı test projesi kullanılıyor: Scootly.Domain.UnitTests (Domain kurallarını,
hiçbir dış bağımlılık olmadan, saniyeler içinde test eder), Scootly.Application.UnitTests
(handler'ları, sahte/fake repository'lerle izole test eder), Scootly.Api.IntegrationTests
(gerçek bir PostgreSQL container'ına karşı, uçtan uca HTTP akışını test eder,
Testcontainers ile testler başında kurulup sonunda otomatik temizlenir).

## Alternatif: Tek bir integration test projesi
Her şeyi gerçek veritabanına karşı test etmek.

## Neden Seçilmedi
Domain kurallarını her test çalıştırmasında gerçek bir veritabanı kurup kaldırarak
test etmek, geri bildirim süresini saniyelerden onlarca saniyeye çıkarır. Sık
çalıştırılan bir test paketinin hızlı olması, geliştiricinin testleri gerçekten
sık çalıştırmasını sağlar — yavaş bir test paketi zamanla atlanır.