# 0004 — Identity: anahtar tipi, şema ve kurulum biçimi

- **Durum:** Kabul edildi
- **Tarih:** 21. gün
- **Bağlam:** Faz 2'nin ilk günü; kullanıcı ve rol altyapısı kuruluyor.

## Karar 1 — Kullanıcı anahtarı `Guid`, `string` değil

ASP.NET Core Identity varsayılan olarak `IdentityUser` kullanır ve birincil anahtarı
`string`'dir (içeride Guid üretilip metne çevrilir). Biz `IdentityUser<Guid>` türeterek
anahtarı `Guid` yaptık.

**Neden:** Alan modelinde kullanıcıya yapılan her atıf zaten `Guid`:
`Ride.DriverId`, `ICurrentUser.UserId`, `ReserveVehicleCommand.DriverId`. String anahtar
seçilseydi her katman sınırında `Guid.Parse` / `ToString()` dönüşümü gerekirdi. Her
dönüşüm, geçersiz girdide çalışma zamanında patlayan ya da — daha kötüsü — sessizce
eşleşmeyen bir nokta demektir. Tip sınırında yapılan dönüşüm sayısını sıfıra indirmek,
22. günde token'dan kimlik okurken ve 24. günde kaynak sahipliği kontrolü yazarken
doğrudan karşılığını verecek.

**Bedeli:** Identity'nin dokümantasyondaki örnekleri `string` anahtar varsayar; kopyala-
yapıştır örnekler birebir uymaz. Kabul edilebilir.

## Karar 2 — Identity tabloları `identity` şemasında

Yedi Identity tablosu (`users`, `roles`, `user_roles`, `user_claims`, `user_logins`,
`user_tokens`, `role_claims`) varsayılan `public` şeması yerine `identity` şemasına
alındı.

**Neden:** 14. günde uygulamanın veritabanına en az yetkiyle bağlanması gerektiğini
konuşmuştuk. Kimlik tabloları alan tablolarından ayrı bir şemadaysa, ileride bu ayrım
veritabanı seviyesinde yetkiye çevrilebilir — örneğin raporlama için açılacak bir okuma
kullanıcısına `public` şemasında `SELECT` verilip `identity` şemasına hiç erişim
verilmeyebilir. Tek şemada bu ayrımı yapmanın yolu tablo tablo yetki vermektir; yeni
tablo eklendiğinde unutulur.

Tablolar yansıma (reflection) döngüsüyle değil, tek tek yazılarak eşlendi. Döngü
kullanılsaydı Identity ileride bir tablo eklediğinde onu sessizce yakalar ve migration'a
kimse fark etmeden girerdi; açık liste derleme zamanında görünür kalıyor.

## Karar 3 — `AddIdentityCore`, `AddIdentity` değil

**Neden:** `AddIdentity`, çerez (cookie) tabanlı oturum şemasını da kurar ve varsayılan
kimlik doğrulama şemasını çereze bağlar. Bu API 22. günden itibaren JWT ile çalışacak;
çerez şeması yalnızca çakışma üretir ve "neden 302 dönüyor" tipinde teşhisi zor
sorunlara yol açar. `AddIdentityCore` yalnızca ihtiyacımız olanı kurar: `UserManager`,
parola hash'leyici, doğrulayıcılar. Rol desteği `AddRoles<ApplicationRole>()` ile
açıkça eklendi.

## Karar 4 — Parola politikası varsayılandan sıkı

Identity varsayılanı 6 karakterdir. 12'ye çıkarıldı; büyük/küçük harf, rakam ve
alfanümerik olmayan karakter zorunlu tutuldu. Hesap kilidi (lockout) 5 başarısız
denemeden sonra 15 dakika.

**Neden:** Parola gücünde belirleyici olan uzunluktur, karakter çeşitliliği değil;
ancak çeşitlilik kuralları en kötü seçimleri eler. Kilit, kaba kuvvet denemesini
pratikte kullanışsız hale getirir. `AllowedForNewUsers` varsayılanda zaten `true`;
açıkça yazıldı ki ileride biri kapatmak isterse bunun bir karar olduğu görünsün.

## Karar 5 — Test kullanıcısının parolası kodda tutulmaz

`IdentitySeeder` rolleri her zaman oluşturur; test kullanıcısını **yalnızca** dışarıdan
bir e-posta ve parola verilmişse oluşturur. Değerler user-secrets'tan okunur.

**Neden:** Koda sabit bir varsayılan parola yazmak, depoyu okuyan herkesin her
kurulumdaki hesabın parolasını bilmesi demektir. Daha kötüsü, böyle bir varsayılan
fark edilmeden üretime kadar taşınır. Parola verilmediğinde kullanıcının hiç
oluşturulmaması bilinçli: yapılandırmayı unutan bir ortamda tahmin edilebilir parolalı
bir hesabın kendiliğinden açılmasını engelliyor.

Aynı gerekçeyle `appsettings.Development.json` içindeki açık metin veritabanı parolası
kaldırıldı ve user-secrets'a taşındı. O değer depoya commit edilmiş durumdaydı; Git
geçmişinde hâlâ duruyor (bkz. teknik borç listesi).

## Değerlendirilen alternatifler

- **Kendi kullanıcı tablomuzu yazmak:** Reddedildi. Parola hash'leme, tuz yönetimi ve
  token üretimi güvenlik açısından hataya en açık alanlardan biri; test edilmiş bir
  kütüphaneye bırakmak doğru tercih.
- **Identity'yi ayrı bir `DbContext`'e koymak:** Reddedildi. İki context, iki bağlantı
  ve iki transaction sınırı demek; 39. günde transaction disiplinini kurarken bu ayrım
  bedava bir karmaşıklık olurdu. Şema ayrımı, aynı faydayı tek context ile veriyor.
