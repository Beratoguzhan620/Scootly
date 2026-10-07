# ADR 0046: 7 Ekim incelemesi sonrası güvenlik ve gizlilik sertleştirmeleri

- Durum: Kabul edildi
- Tarih: 2026-10-07
- İlgili: ADR 0021 (sırlar, cihaz/kullanıcı token ayrımı), ADR 0004 (veri saklama)

## Bağlam

Proje sonu incelemesinde, mevcut borç listesinde olmayan beş güvenlik/gizlilik açığı bulundu:

1. Anonim araç uçları sürüşteki araçların canlı konumunu da döndürüyordu; sürücünün güzergâhı izlenebiliyordu.
2. Mvc harita sayfası tüm API'de geçerli (rolleri taşıyan) bir JWT'yi HTML'e yazıyordu ve Mvc, API'nin imza anahtarını
   taşıyordu; yani her türlü token'ı üretebiliyordu.
3. Verilen JWT'ler iptal edilemiyordu: rol kaldırma veya parola değişikliği 60 dakika etkisizdi.
4. `IdentityBootstrapper`, yapılandırılan e-postaya sahip *var olan* hesabı her açılışta filo yöneticisi yapıyordu. Kayıt
   herkese açık ve e-posta doğrulanmadığı için bu bir yetki yükseltme yoluydu.
5. Api, Worker ve Mvc Postgres'e süper kullanıcıyla bağlanıyordu.

## Karar

1. **Konum görünürlüğü.** Anonim kullanıcılar ve sürücüler listede yalnızca `Available` araçları görür. Tüm filo
   (`FleetOperations`) ve cihaz ağ geçidi (`DeviceOnly`) her durumu görür. Müsait olmayan bir aracın ayrıntısını
   yalnızca onu rezerve etmiş ya da onunla sürüşte olan sürücü görür; diğerleri 404 alır. Mvc haritası veriyi kendi
   ucundan (`/Map/Vehicles`, cookie ile) aynı kurala göre alır.
2. **Hub token'ı.** Ayrı anahtar (`Jwt:HubKey`), ayrı hedef kitle (`Scootly.Hub`), rol taşımaz, 15 dakika geçerlidir.
   Api bunu yalnızca `FleetHub` üzerindeki ikinci şemayla (`HubBearer`) kabul eder; diğer uçlarda varsayılan şema onu
   reddeder. Mvc yalnızca `HubKey`'i bilir, `Jwt:Key`'i bilmez.
3. **Oturum iptali.** Kullanıcı token'ları Identity güvenlik damgasını (`sstamp`) taşır. Damga rol değişikliğinde,
   parola değişikliğinde ve hesap silmede değişir; `UserSessionValidator` her istekte damgayı karşılaştırır. Sonuç
   30 saniye bellekte tutulur, aynı süreçte değişiklik anında temizlenir. Hesap kilitlenmesi bilerek kontrol edilmez:
   başkasının yanlış parola denemeleri açık oturumları düşürmemeli. Mvc cookie'si damgayı 1 dakikada bir doğrular.
4. **Bootstrapper.** Yalnızca bu e-postayla hesap yoksa yöneticiyi oluşturur (hesap + rol tek transaction'da, e-posta
   onaylı). Hesap zaten varsa yetki vermez, uyarı loglar. Yöneticinin bilerek kaldırdığı rol de geri gelmez. Aynı anda
   açılan iki kopyanın yarışı hata değil, bilgi olarak loglanır.
5. **En az yetkili veritabanı rolü.** `db-init` servisi migration'dan sonra `scootly_app` rolünü oluşturur (idempotent)
   ve yalnızca SELECT/INSERT/UPDATE/DELETE ile dizi kullanımı verir. Migration geçmişi salt okunurdur. Api, Worker ve
   Mvc bu rolle bağlanır; süper kullanıcıyı yalnızca migrator ve db-init kullanır.

Ek olarak KVKK kapsamında `GET /api/account`, `POST /api/account/change-password` ve `DELETE /api/account` eklendi.
Silme parolayla onaylanır; açık rezervasyon, devam eden sürüş veya ödenmemiş ücret varken 409 döner; sürüş konumları
hemen anonimleştirilir.

## Ölçülenler

- Api entegrasyon testleri: anonim/sürücü/filo/cihaz görünürlüğü, hub token'ının hub'da 200 ve API'de 401 alması,
  rol kaldırılınca eski token'ın 401 alması, parola değişince eski token'ın 401 ve yeni token'ın 200 alması, hesap
  silme akışı ve engelleri.
- Infrastructure testleri: bootstrapper var olan hesaba yetki vermiyor, yoksa oluşturuyor, tekrar çalışması zararsız.
- Geçici Postgres'te `scootly_app`: okuma, yazma, `FOR UPDATE SKIP LOCKED` çalışıyor; tablo silme, migration
  geçmişini değiştirme ve kendini süper kullanıcı yapma reddediliyor. Betik iki kez çalıştırılabiliyor.
- Geliştirme yığınında uygulamaların 9 bağlantısının hepsi `scootly_app` ile açıldı.

## Ölçülmeyenler

- Güvenlik damgası kontrolünün yük altındaki maliyeti (önbellekli; kullanıcı başına 30 sn'de bir sorgu).
- Çok kopyalı Api'de rol değişikliğinin diğer kopyalara yansıma süresi (tasarım: en fazla 30 sn).

## Sonuçlar ve borç

- Yeni sırlar: `JWT_HUB_KEY`, `APP_DB_USER`, `APP_DB_PASSWORD` (`.env`, `.env.prod`, user-secrets).
- Parola sıfırlama ve e-posta doğrulama bir e-posta sağlayıcısı gerektirdiği için yapılmadı.
- `dotnet run` ile yerel geliştirmede bağlantı dizesi user-secrets'tan gelir ve süper kullanıcı olabilir; rol ayrımı
  compose yığınlarında uygulanır.
