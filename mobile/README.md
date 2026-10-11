# Scootly Mobil

**Scootly sürücü uygulaması: yakındaki scooter'ı bul, rezerve et, sürüşe başla, bitir ve ödemeyi takip et.**
React Native (Expo SDK 57) + TypeScript ile yazıldı; [Scootly API](https://github.com/Beratoguzhan620/Scootly)'sini
kullanır ve web panelinin (Scootly.Mvc) tasarım dilini taşır.

---

## İçindekiler

- [Özellikler](#özellikler)
- [Hızlı Başlangıç](#hızlı-başlangıç)
- [API'ye Bağlanma](#apiye-bağlanma)
- [Komutlar](#komutlar)
- [Proje Yapısı](#proje-yapısı)
- [Tasarım Dili](#tasarım-dili)
- [Backend ile Uyum ve Bilinen Eksikler](#backend-ile-uyum-ve-bilinen-eksikler)
- [Sonraki Adımlar](#sonraki-adımlar)

---

## Özellikler

| Ekran | Ne yapıyor? |
|---|---|
| **Giriş / Kayıt** | `POST /api/auth/login`, `POST /api/auth/register`. Parola kuralları (8+ karakter, A-Z, a-z, 0-9) yazarken canlı gösterilir; sunucunun İngilizce Identity mesajlarına düşülmez. Yalnızca `Driver` rolündeki hesaplar girebilir. |
| **Harita** | Görünen alandaki müsait araçlar (`GET /api/v2/vehicles`, dikdörtgen filtre, 30 sn'de bir yenilenir), hizmet bölgeleri (`GET /api/v1/service-areas`), sürücünün konumu. İşaretler panelin durum renklerinde ve batarya yüzdesini gösterir. |
| **Araç kartı** | Batarya, tahmini menzil, uzaklık. %10'un altındaki araç için rezervasyon kapalı. |
| **Rezervasyon** | 10 dakikalık geri sayım ve ilerleme çubuğu, sürüşü başlat, iptal et. Uygulama kapanıp açılınca rezervasyon geri yüklenir (bkz. [bilinen eksikler](#backend-ile-uyum-ve-bilinen-eksikler)). |
| **Sürüş** | Süre ve tahmini ücret (sunucudaki `Tariff.Standard` ile aynı hesap), sürüşü bitir. Bitiş konumu telefondan; alınamazsa aracın son konumundan. |
| **Sürüş özeti** | Ücret, ödeme durumu (`Pending` → `Paid`/`Failed`) otomatik takip edilir. |
| **Sürüşlerim** | Sayfalı sürüş geçmişi, aşağı çekip yenileme. |
| **Hesap** | Hesap bilgileri, parola değiştirme (sunucunun verdiği yeni token'a geçer), çıkış, KVKK hesap silme. |

Oturum token'ı cihazda şifreli saklanır (iOS Keychain / Android Keystore, `expo-secure-store`). Token süresi
dolunca ya da sunucu oturumu iptal edince (parola/rol değişikliği) kullanıcı bilgilendirilerek giriş ekranına döner.

## Hızlı Başlangıç

**Gerekenler:** Node.js 20.19+ (22 önerilir), çalışan bir Scootly API, telefonda **Expo Go** (SDK 57) ya da
iOS simülatörü / Android emülatörü.

```bash
npm install
cp .env.example .env.local     # gerekirse API adresini yazın
npm start                       # QR kodu Expo Go ile okutun; i = iOS simülatörü, a = Android emülatörü
```

API'yi ayağa kaldırmak için backend reposundaki README → *Hızlı Başlangıç (Docker)*. Kayıt ekranından yeni bir
hesap açmak yeterli; yeni hesaplar otomatik olarak sürücü rolü alır.

## API'ye Bağlanma

Adres şu sırayla belirlenir:

1. `.env.local` içindeki `EXPO_PUBLIC_API_URL`
2. Geliştirmede Metro'nun çalıştığı bilgisayarın adresi + `5016` (telefon Metro'ya ulaşabiliyorsa aynı makinedeki API'ye de ulaşır)
3. `http://localhost:5016`

Geliştirme sürümünde kullanılan adres giriş ekranının altında ve Hesap sekmesinde yazar.

| Uygulama nerede çalışıyor? | `EXPO_PUBLIC_API_URL` | Not |
|---|---|---|
| iOS simülatörü | `http://localhost:5016` | Docker'daki API olduğu gibi çalışır. |
| Android emülatörü | `http://10.0.2.2:5016` | Emülatörde `10.0.2.2`, bilgisayarın kendisidir. |
| Android telefon (USB) | `http://localhost:5016` | Önce `adb reverse tcp:5016 tcp:5016`. |
| Telefon (aynı Wi-Fi) | `http://<bilgisayarın IP'si>:5017` | Aşağıdaki ek ayar gerekir. |

**Fiziksel telefon için:** Backend'in Docker Compose dosyası API'yi yalnızca `127.0.0.1:5016`'ya bağlıyor; telefon
bu adrese ulaşamaz. Backend reposunda, **yalnızca kendi bilgisayarınızda** `deploy/docker-compose.override.yml`
oluşturun (Compose bu dosyayı otomatik okur, repoya eklemeyin):

```yaml
services:
  api:
    ports:
      - "5017:8080"   # yerel ağdan erişim; 5016 bilgisayarın kendisine açık kalır
```

Windows'ta güvenlik duvarına 5017 için gelen bağlantı izni gerekebilir. API bir sanal makinede (ör. Parallels)
çalışıyorsa telefonun ulaşacağı adres o makinenin IP'sidir: ağ modunu *Bridged* yapın ya da port yönlendirme ekleyin.

> Geliştirmede HTTP kullanılıyor. Mağaza derlemeleri için API'nin HTTPS ile yayında olması gerekir (Nginx + TLS
> backend'de hazır).

### Harita anahtarı

Expo Go'da ek ayar gerekmez (iOS'ta Apple Haritalar, Android'de Google Haritalar). Android **geliştirme/üretim
derlemesi** alırken Google Maps SDK anahtarını `GOOGLE_MAPS_ANDROID_API_KEY` ortam değişkeniyle verin; anahtar
`app.config.ts` üzerinden derlemeye girer, repoya yazılmaz.

## Komutlar

| Komut | Ne yapar? |
|---|---|
| `npm start` | Metro'yu başlatır (Expo Go / simülatör). |
| `npm run ios` / `npm run android` | Doğrudan simülatörde/emülatörde açar. |
| `npm run web` | Tarayıcıda önizleme. Harita yerine araç listesi gösterilir; yalnızca arayüzü hızlı denemek içindir. |
| `npm run typecheck` | TypeScript kontrolü. |
| `npm run lint` | ESLint (`eslint-config-expo`). |
| `npm test` | Birim testleri (Jest, `jest-expo`). |
| `npm run check` | Üçü birden — PR öncesi. |

Kendi Xcode derlemenizi almak için `npx expo prebuild` ardından `npx expo run:ios --device`.

## Proje Yapısı

```
src/
├── app/                      # Expo Router ekranları (her dosya bir rota)
│   ├── _layout.tsx           # Sağlayıcılar + korumalı rotalar (oturum açık/kapalı)
│   ├── sign-in.tsx, sign-up.tsx
│   ├── (tabs)/               # Harita, Sürüşlerim, Hesap
│   ├── ride/[id].tsx         # Sürüş özeti ve ödeme takibi
│   └── change-password.tsx, delete-account.tsx
├── api/                      # HTTP istemcisi, sözleşme tipleri, uçlar, React Query kancaları
├── auth/                     # Oturum: token saklama, süre dolumu, sürücü rolü kontrolü
├── ride/                     # Rezervasyon durumu (cihazda saklanır, açılışta doğrulanır)
├── features/                 # Harita kartları ve işaretleri, sürüş geçmişi satırı
├── components/               # Panelin Bootstrap bileşenlerinin karşılıkları
├── domain/                   # Backend kurallarının istemci kopyası (tarife, rezervasyon, parola) + testler
├── lib/                      # Biçimlendirme, coğrafya, JWT, konum, depolama
└── theme/                    # Tasarım dili (renk, tipografi, boşluk, gölge)
```

## Tasarım Dili

Panel Bootstrap 5.3.3'ü varsayılan haliyle kullanıyor; mobil aynı değerleri `src/theme/tokens.ts`'ten alır.

| Panelde | Mobilde |
|---|---|
| `navbar-dark bg-dark` üst menü, solda "Scootly" | Koyu başlık çubuğu ve koyu alt sekme çubuğu; etkin sekme beyaz, diğerleri %55 beyaz |
| Sağda kullanıcı adı (`text-white-50`) | Harita başlığında e-posta |
| `btn-primary`, `btn-outline-*` | `Button` (aynı renkler ve basılı durumlar) |
| `form-label` + `form-control`, `#258cfb` odak halkası (`site.css`) | `TextField` (aynı halka) |
| `card`, `card-header`, `list-group-flush` | `Card`, `ListGroup`, `KeyValueRow` |
| `alert-success` / `alert-danger` (TempData mesajları) | `AlertBox` (Bootstrap 5.3 subtle renkleri) |
| `VehicleStatusTagHelper` rozetleri (Müsait yeşil, Rezerve sarı, Sürüşte mavi, Bakımda gri, Kayıp kırmızı) | `VehicleStatusBadge` ve harita işaretleri — aynı eşleme |
| `<code>` ile 8 karakterlik kısa kimlikler | `Code` |
| `dd.MM.yyyy HH:mm`, `%23` batarya | Aynı biçimler |
| `border-top footer text-muted` — "© 2026 - Scootly" | `PanelFooter` |

Not: `_Layout.cshtml.css` Razor CSS yalıtımıyla yalnızca `_Layout`'un kendi öğelerine uygulanır; oradaki
`.btn-primary { #1b6ec2 }` görünümlerdeki butonlara ulaşmadığı için panelde butonlar Bootstrap'in `#0d6efd`
mavisiyle görünür. Mobil de kullanıcının gördüğü bu rengi kullanır.

## Backend ile Uyum ve Bilinen Eksikler

Uygulama backend'i olduğu gibi kullanıyor; aşağıdakiler mobil için backend tarafında ele alınmaya değer:

1. **Refresh token yok.** JWT 60 dakika geçerli. Süre dolunca uygulama oturumu kapatıp bilgilendiriyor; sürüş
   sunucuda devam ettiği için yeniden girişte kaldığı yerden sürer. Kalıcı çözüm: refresh token ucu.
2. **"Aktif rezervasyonum" ucu yok.** `GET /api/rides/active` var ama rezervasyon için karşılığı yok. Uygulama
   rezerve ettiği aracı cihazda saklayıp açılışta araç ayrıntısıyla doğruluyor. Başka cihazdan yapılan
   rezervasyonu göremez (yeni rezervasyon denemesi sunucudan "zaten aktif rezervasyonunuz var" döner).
   Öneri: `GET /api/reservations/active` (araç kimliği + bitiş zamanı).
3. **Rota sürümleri karışık.** `vehicles` ve `service-areas` sürümlü, `auth`, `account`, `rides` sürümsüz
   (`docs/backlog/technical-debt.md`'de de not edilmiş). Mobil mağazaya çıkmadan düzeltmek en ucuzu.
4. **Araç listesi IP başına dakikada 60 istekle sınırlı** (`Anonymous` politikası, oturum açmış sürücüler için de).
   Mobil operatörlerde çok sayıda kullanıcı aynı IP'yi paylaşabilir; oturum açmış istekler için kullanıcı bazlı
   bölümleme düşünülmeli.
5. **Tarife istemcide sabit.** Tahmini ücret `Tariff.Standard`'ın kopyasıyla (`src/domain/rules.ts`) hesaplanıyor;
   kesin ücret sunucudan geliyor. Tarife değişebilir olacaksa bir `GET /api/tariff` ucu iyi olur.
6. **Identity hata mesajları İngilizce.** İstemci kuralları önceden denetlediği için kullanıcı bunları görmüyor;
   yine de Türkçe bir `IdentityErrorDescriber` diğer istemciler için faydalı olur.

## Sonraki Adımlar

- `FleetHub` (`VehicleStatusChanged`) ile canlı harita: bir araç rezerve edilince haritadan anında düşsün.
- Ödeme sonucu için push bildirimi.
- Uygulama simgesi ve açılış ekranı (şu an Expo varsayılanları).
- Kritik akışlar için uçtan uca test (ör. Maestro).
