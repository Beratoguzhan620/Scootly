# ADR 0028: MVC Güvenlik Kararları (90. Gün)

## Durum
Kabul edildi. (Numaralandırma notu: dokümanın önerdiği 0026, 86A/87/88'de
sırasıyla FieldOps, arayüz yetkilendirmesi ve MVC-API kimlik kararlarına
verildiği için bu karar 0028 olarak kaydedildi.)

## XSS Doğrulaması
Araç marka alanına `<script>alert('XSS')</script>` yazılıp kaydedildi;
listede düz metin olarak göründü, hiçbir kod çalışmadı. Projede `Html.Raw`
kullanımı yok — Razor'ın varsayılan otomatik kaçışlaması tek koruma katmanı
ve yeterli bulundu.

## Content Security Policy
İlk denemede iki ihlal bulundu ve düzeltildi:
1. `script-src 'self'` — `Views/Map/Index.cshtml`'deki satır içi
   `<script>` bloğu (token/API adresini JS'e aktarıyordu) engellendi.
   **Çözüm:** Değerler `data-*` HTML özniteliklerine taşındı, dış JS
   dosyası (`map.js`) bunları okuyor — satır içi script tamamen kaldırıldı.
2. `style-src 'self'` — Leaflet kütüphanesinin kendi iç mantığı, harita
   elemanlarına satır içi stil uyguluyor. **Çözüm:** `style-src`'ye
   `'unsafe-inline'` eklendi (yalnızca style, script'e eklenmedi —
   gerçek XSS koruması `script-src 'self'` üzerinde kalmaya devam ediyor).

Son politika:
default-src 'self'; script-src 'self'; style-src 'self' 'unsafe-inline';
img-src 'self' data: https://*.tile.openstreetmap.org;
connect-src 'self' http://localhost:5016 ws://localhost:5016;
## Diğer Güvenlik Başlıkları
`X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`,
`Referrer-Policy: strict-origin-when-cross-origin` eklendi.

## Cookie Güvenliği
Hem kimlik (Identity) hem session cookie'sine `HttpOnly`, `SameSite=Lax`
eklendi. `SecurePolicy`, ortam bazlı: geliştirmede (`http://localhost`)
`SameAsRequest` (yoksa HTTPS olmadan cookie hiç ayarlanmaz, giriş
imkansız hale gelir), üretimde `Always` zorunlu.

## Doğrulama
Her değişiklikten sonra harita (fetch + SignalR WebSocket) ve giriş/çıkış
akışı gerçek tarayıcıda yeniden test edildi — hiçbir meşru işlevsellik
CSP veya cookie değişiklikleriyle bozulmadı.