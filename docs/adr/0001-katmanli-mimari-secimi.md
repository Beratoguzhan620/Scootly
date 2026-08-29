# 0001 — Katmanlı mimari ve bağlam bazlı klasörleme

Tarih: 2026-08-29
Durum: Kabul edildi

## Bağlam

Scootly 22 haftalık bir geliştirme sürecinde büyüyecek: mesajlaşma, arka plan
servisleri, MVC arayüzü ve modüler mimari eklenecek. Ekip üç kişi ve herkes
aynı kod tabanında paralel çalışıyor. Başlangıçta seçilecek kod düzeni,
ilerideki her eklemenin maliyetini belirleyecek.

## Karar

Clean Architecture katmanlaması (Domain / Application / Infrastructure / Api)
ve her katman içinde bağlam bazlı klasörleme (Fleet, Riding, Geo) seçildi.
Bağımlılık yönü tek yönlü ve derleme zamanında zorunlu: Domain hiçbir projeye
referans vermez, herkes Domain'e referans verir.

## Değerlendirilen alternatifler

**Vertical Slice** — her özelliği (rezervasyon, sürüş başlatma) kendi klasöründe,
istek-yanıt-handler-veri erişimi bir arada tutan yaklaşım. Özellik eklemeyi
hızlandırır ve dosyalar arası gezinmeyi azaltır. Seçilmedi, çünkü bu projede
öğrenme hedefi katman sınırlarını ve bağımlılık yönünü **hissetmek**; Vertical
Slice bu sınırları bilinçli olarak bulanıklaştırıyor. Ayrıca 16. haftadaki
mimari kural testi (bağımlılık ihlallerini yakalayan test) katmanlı yapıyı
varsayıyor.

**Tek projeli monolit** — tüm kodu tek bir projede tutmak. Başlangıçta en hızlısı,
ama bağımlılık yönü derleyici tarafından zorlanamaz; Domain'in Infrastructure'ı
tanımasını hiçbir şey engellemez. 13. haftada modüler mimariye geçerken
sökülmesi çok pahalı olurdu.

## Sonuçlar

**Olumlu:** Katman ihlalleri derleme zamanında yakalanır. Domain katmanı
veritabanı ve HTTP olmadan test edilebiliyor — 53 birim testi saniyeler içinde
çalışıyor.

**Olumsuz:** Basit bir özellik eklemek dört ayrı projeye dokunmayı gerektiriyor.
Bir uç eklemek için komut, handler, controller ve sözleşme tipleri ayrı ayrı
yazılıyor. Bu maliyet bilinçli kabul edildi.