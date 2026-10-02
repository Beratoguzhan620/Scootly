# ADR 0029: Faz 5 İlk Yarı Retro (81-90. Gün)

## Durum
Kabul edildi — checkpoint dokümanı.

## Tamamlanan Haftalar
- **Hafta 17 (81-85):** Scootly.Mvc kuruldu, cookie kimlik doğrulama,
  araç CRUD, Redis session, sayfalama.
- **Hafta 18 (86A, 86-90):** FieldOps bağlamı (ek gün), view component'ler
  ve rol bazlı paneller, rol bazlı navigasyon + hata sayfaları, Leaflet
  harita, SignalR canlı güncelleme, arayüz güvenliği.

## Sapmalar ve Düzeltmeler
- 86A gerçekten eksikti (FieldTask hiç yazılmamıştı) — dokümanın öngördüğü
  gibi ek gün olarak eklendi.
- ADR numaralandırması dokümanın önerdiğinden kaydı: 0025→FieldOps,
  0026→arayüz yetkilendirmesi, 0027→MVC-API kimlik, 0028→güvenlik.
- 82. günün gerçek kapsamı (Infrastructure refactor + AccountController)
  ilk seferde eksik yapılmıştı, geriye dönüp tamamlandı.
- İki ayrı migration geri alma/yeniden uygulama olayı yaşandı (86A'da
  CreatedAt sütunu eksikliği) — ikisi de düzeltildi, veri kaybı olmadı.

## Test Sağlığı
213 → 231 teste çıkıldı (Hafta 17-18 boyunca eklenen testler), tamamı
yeşil, her gün sonunda doğrulandı.

## Gerçek Tarayıcı Doğrulamaları
Her özellik (giriş/çıkış, CRUD, sayfalama, rol bazlı erişim/403, harita,
SignalR canlı güncelleme, XSS koruması, CSP) gerçek tarayıcıda, gerçek
verilerle test edildi — yalnızca birim/entegrasyon testlerine güvenilmedi.

## Sonraki Adım
Hafta 19: Gözlemlenebilirlik (91. gün — yapılandırılmış loglama, Seq,
korelasyon ID'leri).