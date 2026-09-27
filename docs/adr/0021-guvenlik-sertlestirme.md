# ADR 0021: Güvenlik Sertleştirmesi — Sırlar, Kimlikler ve Erişim Sınırları

## Durum
Kabul edildi (27.09.2026).

## Bağlam
Teknik incelemede şu açıklar bulundu:

- JWT imza anahtarı, cihaz sırrı ve DB parolası Git geçmişindeydi, depo herkese açıktı ve aynı değerler hâlâ
  kullanımdaydı: herkes `FleetManager` rolünde veya başka bir kullanıcı adına token üretebilirdi. Cihaz sırrı
  DeviceSimulator'ın, webhook sırrı ise Infrastructure'ın kaynak kodundaydı.
- Herhangi bir kullanıcı başkasının sürüşünü bitirebiliyor, başkasının rezerve ettiği araçla sürüş başlatabiliyordu.
- Cihaz ve kullanıcı token'ları ayrılmıyordu: her kullanıcı sahte telemetri gönderebiliyor, cihaz token'ı kullanıcı
  uçlarında 500 hatası üretiyordu.
- Rate limiting global'di (tek istemci herkesi kilitleyebiliyordu), girişte kilitleme yoktu, hatalarda iç ayrıntı sızıyordu.

## Karar

**Sırlar**
- Hiçbir sır kaynak kodda veya appsettings'te tutulmaz. Geliştirmede `dotnet user-secrets`, container'da ortam
  değişkenleri, docker-compose için `deploy/.env` (git'e girmez, şablonu `.env.example`) kullanılır.
- Tüm ayarlar Options pattern ile bağlanır ve açılışta doğrulanır (`ValidateOnStart`): eksik ya da kısa bir anahtar
  uygulamanın hiç açılmamasına yol açar (fail fast). Anahtarlar en az 32 karakterdir.
- Sızan değerlerin tamamı 27.09.2026'da yenilendi (JWT, cihaz, webhook, PostgreSQL, RabbitMQ; Redis'e parola eklendi).
  Geçmişteki eski değerler artık hiçbir yerde geçerli değildir. Geçmişin yeniden yazılması (`git filter-repo`) paylaşılan
  dalları bozacağı için yapılmadı; rotasyon, sızıntının etkisini ortadan kaldırmak için yeterli ve zorunlu olan adımdır.

**Kimlikler ve yetkiler**
- Varsayılan olarak güvenli: `[AllowAnonymous]` olmayan her uç kimlik doğrulaması ister (fallback policy).
- Token'lar `client_type` (user/device) taşır. Kullanıcı uçları `DriverOnly`, filo uçları `FleetManagerOnly` /
  `FleetOperations`, telemetri `DeviceOnly` politikasıyla korunur.
- Roller (Driver, FleetManager, FieldOperator) migration ile sabit kimliklerle tohumlanır; yeni kullanıcılar Driver olur.
  İlk filo yöneticisi `Bootstrap:*` ayarlarıyla oluşturulur; diğer roller `/api/v1/admin/users/{id}/roles/{rol}` ile atanır.
- Sahiplik: sürüş okuma/bitirme sahibine özeldir; başkasına ait kaynak için `404` döner (varlık açığa çıkmaz).
  Rezervasyon sahibi araçta tutulur; yalnızca o sürücü sürüş başlatabilir veya iptal edebilir.
- Cihazlar **ağ geçidi modeli** ile doğrulanır: tek bir istemci kimliği birden fazla aracın telemetrisini gönderebilir,
  ancak her okuma kayıtlı bir araca ait olmalıdır. Sır karşılaştırması sabit sürelidir ve yapılandırma eksikse hiçbir
  istemci doğrulanmaz.

**Kötüye kullanıma karşı**
- Rate limiting istemci başına bölümlenir: anonim uçlar IP başına, giriş/kayıt IP başına sıkı sınır, yazma işlemleri
  kullanıcı başına, telemetri cihaz başına. Aşımda `429 + Retry-After`.
- 5 başarısız girişte hesap 15 dakika kilitlenir; bilinmeyen kullanıcı ve yanlış parola aynı yanıtı ve benzer süreyi alır;
  kayıt ucu hesabın varlığını açığa çıkarmaz. Parola en az 8 karakter, büyük/küçük harf ve rakam içerir.
- Hatalar RFC 7807 ProblemDetails olarak döner; beklenmeyen hatalarda ayrıntı loglanır, istemciye yalnızca genel mesaj gider.
- Loglarda hassas alanlar (parola, sır, token, imza vb.) özellik adına göre maskelenir.
- docker-compose portları yalnızca `127.0.0.1`'e açılır.

## Alternatif: Araç başına cihaz kimliği
Her aracın kendi kimlik bilgisiyle token alması ve yalnızca kendi telemetrisini gönderebilmesi.

## Neden Şimdilik Seçilmedi
Cihaz kayıt/provizyon akışı, sır dağıtımı ve rotasyonu gerektirir; mevcut simülatör bir ağ geçidi gibi davranıyor.
Ağ geçidi modeli, kayıtlı araç doğrulaması ve cihaz başına rate limiting ile birlikte bu aşama için yeterli. Gerçek cihaz
filosuna geçişte araç başına kimlik (veya mTLS) değerlendirilmeli — teknik borç listesinde.
