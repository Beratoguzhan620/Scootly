# Scootly Mobil

**Scootly sürücü uygulaması: yakındaki scooter'ı bul, rezerve et, sürüşe başla, bitir ve ödemeyi takip et.**
React Native (Expo SDK 57) + TypeScript ile yazıldı; [Scootly API](https://github.com/Beratoguzhan620/Scootly)'sini
kullanır. Görünüm "sokak tabelası" fikrine dayanır: voltaj sarısı levhalar, derin lacivert yazı, vektör harita
(bkz. [Tasarım Dili](#tasarım-dili)).

---

## İçindekiler

- [Android APK (test sürümü)](#android-apk-test-sürümü)
- [Özellikler](#özellikler)
- [Hızlı Başlangıç (geliştirme)](#hızlı-başlangıç-geliştirme)
- [API'ye Bağlanma](#apiye-bağlanma)
- [Harita](#harita)
- [Komutlar](#komutlar)
- [CI ve APK Derleme](#ci-ve-apk-derleme)
- [Proje Yapısı](#proje-yapısı)
- [Tasarım Dili](#tasarım-dili)
- [Backend ile Uyum ve Bilinen Eksikler](#backend-ile-uyum-ve-bilinen-eksikler)
- [Sonraki Adımlar](#sonraki-adımlar)

---

## Android APK (test sürümü)

`main`'e her push'ta GitHub Actions APK derler, Android emülatöründe ana akışı dener ve sonucu
**Releases → "Android test sürümü (son derleme)"** (`android-latest`) altına koyar.

1. Releases sayfasını telefonda açıp `scootly.apk` dosyasını indirin ve açın. İlk kurulumda tarayıcıya
   "bilinmeyen uygulamaları yükleme" izni vermeniz istenir. Yeni derlemeler eskisinin üzerine kurulur.
2. API'yi telefonun erişebileceği şekilde çalıştırın (bkz. [API'ye Bağlanma](#apiye-bağlanma)).
3. Uygulamada giriş ekranının altındaki **Sunucu: … Değiştir** bağlantısından bilgisayarın adresini girin
   (ör. `http://192.168.1.20:5017`), **Bağlantıyı test et** ile doğrulayıp kaydedin.

APK hata ayıklama anahtarıyla imzalıdır ve HTTP bağlantısına izin verir; mağaza sürümü değildir.

## Özellikler

| Ekran | Ne yapıyor? |
|---|---|
| **Giriş / Kayıt** | `POST /api/auth/login`, `POST /api/auth/register`. Parola kuralları (8+ karakter, A-Z, a-z, 0-9) yazarken canlı gösterilir; sunucunun İngilizce Identity mesajlarına düşülmez. Yalnızca `Driver` rolündeki hesaplar girebilir. |
| **Sunucu ayarları** | API adresi uygulama içinden değiştirilir ve cihazda saklanır; `/health/ready` ile bağlantı denenir. IP değişince yeniden derlemek gerekmez. |
| **Harita** | MapLibre ile vektör harita (OpenFreeMap karoları, uygulamanın renklerine boyanır; açık ve koyu tema). Görünen alandaki müsait araçlar (`GET /api/v2/vehicles`, dikdörtgen filtre, 30 sn'de bir yenilenir) batarya halkalı sarı işaretlerle, uzaklaştıkça kümelenerek; hizmet bölgeleri (`GET /api/v1/service-areas`), sürücünün konumu. **En yakın scooter'ı göster** kiralanabilir en yakın aracı seçer. |
| **Araç paneli** | Batarya halkası, tahmini menzil, uzaklık ve yürüme süresi. %10'un altındaki araç için rezervasyon kapalı. Panel aşağı çekilince kapanır; seçilen araç panelin altında kalırsa harita ona kayar. |
| **Rezervasyon** | 10 dakikalık geri sayım (sarı levha) ve ilerleme çubuğu, sürüşü başlat, iptal et. Panel aşağı çekilince yalnızca sayaç kalır. Uygulama kapanıp açılınca rezervasyon geri yüklenir (bkz. [bilinen eksikler](#backend-ile-uyum-ve-bilinen-eksikler)). |
| **Sürüş** | Lacivert panelde süre ve tahmini ücret (sunucudaki `Tariff.Standard` ile aynı hesap), sürüşü bitir. Bitiş konumu telefondan; alınamazsa aracın son konumundan. |
| **Sürüş özeti** | Büyük ücret, başlangıç–bitiş zaman çizelgesi; ödeme durumu (`Pending` → `Paid`/`Failed`) otomatik takip edilir. |
| **Sürüşlerim** | Aylara göre gruplanmış, sayfalı sürüş geçmişi ("Bugün 14:32"), aşağı çekip yenileme. |
| **Hesap** | Hesap bilgileri, parola değiştirme (sunucunun verdiği yeni token'a geçer), çıkış, KVKK hesap silme. |

Oturum token'ı cihazda şifreli saklanır (iOS Keychain / Android Keystore, `expo-secure-store`). Token süresi
dolunca ya da sunucu oturumu iptal edince (parola/rol değişikliği) kullanıcı bilgilendirilerek giriş ekranına döner.

## Hızlı Başlangıç (geliştirme)

**Gerekenler:** Node.js 20.19+ (22 önerilir), çalışan bir Scootly API, telefonda **Expo Go** (SDK 57) ya da
iOS simülatörü / Android emülatörü.

```bash
npm install
cp .env.example .env.local     # gerekirse varsayılan API adresini yazın
npm start                       # QR kodu Expo Go ile okutun; i = iOS simülatörü, a = Android emülatörü
```

API'yi ayağa kaldırmak için backend reposundaki README → *Hızlı Başlangıç (Docker)*. Kayıt ekranından yeni bir
hesap açmak yeterli; yeni hesaplar otomatik olarak sürücü rolü alır.

API olmadan arayüzü denemek için sahte API: `python3 e2e/mock-api.py --port 5999`
(giriş: `surucu@scootly.test` / `Scootly2026`; adres emülatörde `http://10.0.2.2:5999`).

## API'ye Bağlanma

Varsayılan adres şu sırayla belirlenir; uygulama içindeki sunucu ayarları hepsinin önüne geçer:

1. `EXPO_PUBLIC_API_URL` (`.env.local` ya da derleme ortamı)
2. Geliştirmede Metro'nun çalıştığı bilgisayarın adresi + `5016` (telefon Metro'ya ulaşabiliyorsa aynı makinedeki API'ye de ulaşır)
3. `http://localhost:5016`

| Uygulama nerede çalışıyor? | API adresi | Not |
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

API'yi `dotnet run` ile çalıştırıyorsanız yerel ağa açmak için:
`dotnet run --project src/Scootly.Api --urls http://0.0.0.0:5016` (adres bu durumda `:5016`).

### Backend'i bu Mac'te tek komutla çalıştırma

VS Code: *Terminal → Run Task… → **Backend'i başlat (Docker)*** (ya da `bash scripts/mac/backend.sh`). Docker Desktop
gerekir; betik:

1. Backend'i `~/scootly-backend`'e indirir (`SCOOTLY_BACKEND_DIR` ile değiştirilebilir; var olan kopyaya dokunmaz).
2. `deploy/.env` yoksa `.local-build/backend.env`'yi oraya taşır; o da yoksa anahtarları rastgele üretir ve yalnızca
   panel yöneticisinin e-posta/parolasını sorar. Sırlar bu repoya hiç girmez.
3. Yukarıdaki override dosyasını yazar (API `5017`'den yerel ağa açılır). Apple Silicon'da migrator imajını arm64
   için derleyen yerel bir Dockerfile da ekler: takımın `src/Scootly.Api/Dockerfile`'ı migration paketini
   `-r linux-x64` ile ürettiği için arm64 imajında çalışmıyor (kalıcı çözüm için backend'de `TARGETARCH`'a göre RID
   seçilmeli). Bu iki dosya backend reposunda `.git/info/exclude` ile git dışında tutulur.
4. `docker compose --profile app up -d --build` ile yığını açar, `/health/ready`'yi bekler.
5. Veritabanında hiç araç yoksa yönetici hesabıyla iki hizmet bölgesi ve 10 örnek scooter ekler: bir küme ilk
   kurulumda sorulan konumun çevresine (telefonda haritayı açınca hemen görünsün diye), bir küme panelin varsayılan
   harita merkezine (İstanbul). Pil düzeyleri kuralları da gösterir: %15 (düşük, saha görevi açılır) ve %7 (kiralanamaz).
6. API'yi Cloudflare hızlı tüneliyle internete de açar (hesap gerekmez, `cloudflare/cloudflared` konteyneri):
   `https://<rastgele>.trycloudflare.com` adresi mobil veride ve başka ağlarda da çalışır. Adres tünel her yeniden
   başladığında değişir; kalıcı bir adres için adlandırılmış bir tünel (Cloudflare hesabı + alan adı) ya da
   backend'in bir sunucuya kurulması gerekir. Kapatmak için `SCOOTLY_TUNNEL=0`.
7. Telefona yazılacak adresleri yazdırır: aynı Wi-Fi'dan `http://<Mac'in IP'si>:5017`, her yerden tünel adresi.

> Tünel açıkken API internete açıktır. Geliştirme yığınının sırları (JWT anahtarı, yönetici parolası) ekipte
> paylaşılan değerlerse, adresi geniş bir kitleyle paylaşmadan önce `deploy/.env`'de değiştirin. Mac uykuya geçerse
> ya da kapanırsa bağlantı kopar.

Diğer görevler: **Backend durumu** (adresler ve tünelin erişilebilirliği), **Tüneli yenile (yeni adres)**: hızlı
tünelin bağlantısı uzun süre koparsa (Mac uykuya geçti, ağ değişti) Cloudflare onu siler ve loglarda
`Tunnel not found` görünür; bu görev yeni bir tünel ve adres açar. **Backend'i durdur** (veriler Docker volume'unda kalır). Çıktı
`.local-build/backend.log`'a da yazılır. Uygulamaya yalnızca `Driver` rolündeki hesaplar girebildiği için telefonda
**Kayıt ol** ile bir sürücü hesabı açın; yönetici hesabı web paneli (`http://127.0.0.1:5096`) içindir.

Bilgisayarın IP'si: macOS'ta *Sistem Ayarları → Wi-Fi → Ayrıntılar*, Windows'ta `ipconfig`. Güvenlik duvarı gelen
bağlantıyı sorabilir; izin verin. API bir sanal makinede (ör. Parallels) çalışıyorsa telefonun ulaşacağı adres o
makinenin IP'sidir: ağ modunu *Bridged* yapın ya da port yönlendirme ekleyin.

> Test sürümlerinde HTTP kullanılıyor. Mağaza sürümünde API HTTPS ile yayınlanır; derleme
> `SCOOTLY_ALLOW_HTTP=false` ve `EXPO_PUBLIC_LOCK_API_URL=true` ile alınır (sunucu ayarları gizlenir).

## Harita

Harita **MapLibre GL JS 5** ile vektör karolardan bir WebView içinde çizilir (`src/features/map`). Google ya da Apple
harita anahtarı gerekmez; Expo Go'da, kendi derlemelerimizde ve web önizlemesinde aynı sayfa çalışır. MapLibre
uygulamaya gömülüdür (`npm run embed:maplibre` ile üretilir), CDN'e bağımlı değildir.

- **Altlık:** [OpenFreeMap](https://openfreemap.org)'in "Positron" stili (OpenStreetMap verisi, API anahtarı ve
  kullanım sınırı yok). Stil sayfada uygulamanın renklerine yeniden boyanır (`mapTheme.ts`): açık temada serin gri
  zemin ve beyaz yollar, koyu temada lacivert gece. Başka bir MapLibre stili için `EXPO_PUBLIC_MAP_STYLE_URL`.
  Stil alınamazsa araçlar düz zeminde görünmeye devam eder, stil arka planda yeniden denenir.
- **İşaretler:** sarı levha, içinde lacivert batarya yayı ve yüzde; düşük bataryada yay kırmızı, kiralanamayan
  araç gri. Seçilen araç büyüyüp bir direğe oturur. Yakın araçlar uzaklaştıkça kümelenir; kümeye dokununca
  yakınlaşır. İşaretlerdeki rakamlar uygulamayla aynı yazı tipinden (`mapFont.generated.ts`, yalnızca rakamlar).
- **Köprü:** uygulama ile sayfa JSON mesajlarıyla konuşur (`mapBridge.ts`): uygulama araçları, hizmet bölgelerini,
  konumu, alttaki panelin yüksekliğini ve temayı gönderir; sayfa görünen alanı, seçilen aracı ve boş alana
  dokunmayı bildirir. Sayfanın mantığı jsdom içinde sahte bir MapLibre ile test edilir (`mapPage.test.ts`).

Harita WebGL ile çizildiği için işaretler ekran okuyucuda tek tek görünmez; **En yakın scooter'ı göster** düğmesi
ve araç paneli aynı bilgiyi erişilebilir biçimde verir.

## Komutlar

| Komut | Ne yapar? |
|---|---|
| `npm start` | Metro'yu başlatır (Expo Go / simülatör). |
| `npm run ios` / `npm run android` | Doğrudan simülatörde/emülatörde açar. |
| `npm run web` | Tarayıcıda önizleme (harita dahil). Yalnızca arayüzü hızlı denemek içindir. |
| `npm run typecheck` | TypeScript kontrolü. |
| `npm run lint` | ESLint (`eslint-config-expo`). |
| `npm test` | Birim testleri (Jest). Harita sayfası jsdom içinde gerçekten çalıştırılarak test edilir. |
| `npm run check` | Üçü birden — PR öncesi. |
| `npm run embed:maplibre` | MapLibre sürümü değişince gömülü dosyaları yeniden üretir. |
| `python3 scripts/subset-map-font.py` | Harita işaretlerindeki rakamların yazı tipini yeniden üretir (fonttools gerekir). |

## CI ve APK Derleme

`.github/workflows/android.yml` her push ve PR'da çalışır:

| İş | Ne yapar? |
|---|---|
| **Tip, lint ve testler** | `npm run check` |
| **APK derle** | `expo prebuild` ile Android projesini üretir, React Native'in beklediği NDK'yı kurar, `assembleRelease` (armeabi-v7a, arm64-v8a, x86_64). Hata olursa Gradle'ın özeti iş notlarına düşer. |
| **Emülatörde duman testi** | Android 14 emülatöründe sahte API'ye (`e2e/mock-api.py`) karşı giriş → harita → araç seçimi → rezervasyon → sürüş → ödeme → geçmiş → hesap akışını ekrandaki metinlere dokunarak gezer (`e2e/android-smoke.py`), her adımın ekran görüntüsünü alır, çökme kaydı varsa başarısız olur. |
| **Releases'a yayınla** | Yalnızca `main`: APK'yı ve emülatör ekran görüntülerini `android-latest` ön sürümüne koyar. |

İsteğe bağlı: depo ayarlarında *Settings → Secrets and variables → Actions → Variables* altında
`EXPO_PUBLIC_API_URL` tanımlanırsa APK bu adresle başlar.

### macOS'ta yerel derleme

VS Code'da **Terminal → Run Build Task…** (⇧⌘B) ya da terminalde:

```bash
bash scripts/mac/build-apk.sh                    # telefonlar için (arm64-v8a, armeabi-v7a)
ABIS=arm64-v8a bash scripts/mac/build-apk.sh     # yalnızca 64 bit: daha hızlı
```

Betik eksik araçları yönetici parolası istemeden kullanıcı klasörlerine kurar: Node.js 22 (sistemde uygun sürüm
yoksa), JDK 17 (`~/Library/Java/JavaVirtualMachines/temurin-17.jdk`), Android SDK + NDK (`~/Library/Android/sdk`,
Android Studio'nun da kullandığı yer). Android SDK lisanslarını kabul edip etmeyeceğini ilk çalıştırmada sorar.
İlk kurulum ~8 GB disk ve indirme hızına göre 20-30 dakika sürer; sonraki derlemeler birkaç dakikadır.
Sonuç proje kökünde `scootly.apk`; telefon USB hata ayıklamayla bağlıysa kurmayı da önerir. Tüm çıktı
`.local-build/build.log` dosyasına yazılır.

### Diğer sistemler

Android Studio (JDK ve Android SDK ile birlikte gelir) kurulu olmalı:

```bash
npx expo prebuild --platform android --clean
cd android && ./gradlew assembleRelease      # Windows: gradlew.bat assembleRelease
# → android/app/build/outputs/apk/release/app-release.apk
```

Windows'ta C++ derleme yolları uzun olduğu için projeyi kısa bir yolda tutun (ör. `C:\dev\scootly-mobile`).
Mağaza sürümü için kendi imza anahtarınız gerekir (EAS Build ya da Gradle `signingConfig`).

## Proje Yapısı

```
src/
├── app/                      # Expo Router ekranları (her dosya bir rota)
│   ├── _layout.tsx           # Sağlayıcılar + korumalı rotalar (oturum açık/kapalı)
│   ├── sign-in.tsx, sign-up.tsx, server.tsx
│   ├── (tabs)/               # Harita, Sürüşlerim, Hesap
│   ├── ride/[id].tsx         # Sürüş özeti ve ödeme takibi
│   └── change-password.tsx, delete-account.tsx
├── api/                      # HTTP istemcisi, sözleşme tipleri, uçlar, React Query kancaları, sunucu adresi
├── auth/                     # Oturum: token saklama, süre dolumu, sürücü rolü kontrolü
├── ride/                     # Rezervasyon durumu (cihazda saklanır, açılışta doğrulanır)
├── features/map/             # MapLibre harita sayfası, köprü, WebView/iframe kabukları, alt paneller
├── features/rides/           # Sürüş geçmişi satırı
├── components/               # Ortak bileşenler: buton, form alanı, alt panel, sekme çubuğu, batarya halkası…
├── domain/                   # Backend kurallarının istemci kopyası (tarife, rezervasyon, parola) + testler
├── lib/                      # Biçimlendirme, coğrafya, JWT, konum, depolama, adres doğrulama
└── theme/                    # Tasarım dili: açık/koyu renkler, Barlow yazı tipleri, ölçek, boşluklar
e2e/                          # Sahte API ve Android duman testi (CI)
scripts/                      # MapLibre'yi gömen betik, harita yazı tipi, macOS'ta APK ve backend betikleri
.github/workflows/            # Android APK hattı
```

## Tasarım Dili

Fikir **sokak tabelası**: voltaj sarısı levhalar, üstlerinde derin lacivert yazı, önemli sayılar dar ve büyük.
Sarı ekranda "burada işlem yap" demektir (seçili scooter, ana buton, etkin sekme); geri kalan her şey sakin.

| | |
|---|---|
| **Renkler** | Voltaj sarısı `#FFD60A` (yalnızca dolgu, üstünde her zaman lacivert yazı), derin lacivert `#0B1E4A`, serin kâğıt zemini `#F3F5F9`, beyaz yüzeyler. Koyu temada lacivert gece (`#0A1428`); sarı aynı kalır. Tümü `src/theme/palette.ts`'te; telefonun açık/koyu ayarını izler. |
| **Yazı** | [Barlow](https://fonts.google.com/specimen/Barlow) (arayüz; Kaliforniya karayolu tabelalarından türetilmiş) ve Barlow Condensed (batarya, sayaç, ücret, büyük başlıklar ve logo). Türkçe karakterler ve ₺ dahil. Butonlar ve başlıklar cümle düzeninde ("Sürüşü başlat"). |
| **Harita ekranı** | Başlık yok; harita tam ekran. Tek bir alt panel durumdan duruma geçer: yakındakiler → seçilen araç → rezervasyon → sürüş. Sürüş paneli ters renkli (lacivert zemin, sarı sayaç). Konum ve yenileme düğmeleri panelle birlikte hareket eder. |
| **Kimlikler** | Araç ve sürüş kodları numara plakası gibi (`Plate`): çerçeveli, dar rakamlar. |
| **Köşeler** | Hiyerarşiye göre: alt panel 28, yüzeyler 20, butonlar ve alanlar 14, plakalar 8. Gölge yalnızca yüzen öğelerde. |
| **Hareket** | Yalnızca bir eylemin sonucunu gösterir: panelin kayması, seçilen işaretin büyümesi, basılan butonun küçülmesi, dokunsal geri bildirim. Sistemde "hareketi azalt" açıksa animasyonlar atlanır. |
| **Simge** | Sarı levha üstünde lacivert scooter piktogramı (Android'de uyarlanabilir ve temalı simge). Açılış ekranı aynı sarıda. |

Durum rozetlerinin eşlemesi web paneliyle aynı (`domain/labels.ts`: Müsait, Rezerve, Sürüşte, Bakımda, Kayıp);
yalnızca görünümleri yeni dile uyarlandı.

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
- Mağaza sürümü: HTTPS API, kendi imza anahtarı, ticari karo sağlayıcısı.
