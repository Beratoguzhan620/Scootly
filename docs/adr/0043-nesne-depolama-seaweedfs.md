# ADR 0043: Nesne depolama icin SeaweedFS (S3 API), MinIO degil

- Durum: Kabul edildi (Gun 108b)
- Tarih: 2026-10-06

## Baglam

Gun 108 plani saha gorevi tamamlanirken fotograf yuklemeyi ve ona on-imzali URL ile erisimi ister. Plan nesne deposu olarak MinIO'yu one surer (compose, sabit surum, 127.0.0.1).

Uygulamaya baslarken MinIO community surumunun arsivlendigi (25.04.2026) ve Docker Hub imajlarinin silindigi (Eylul 2026) goruldu. Sabit surumlu, kaynagi belli bir imaj bulunamadigi icin plandaki secenek uygulanamaz hale geldi. Karar kullanici ile birlikte verildi.

## Karar

- Depo: SeaweedFS, imaj `chrislusf/seaweedfs:4.48` (digest compose dosyasinda yorum olarak kayitli), compose profili `storage`, yalnizca `127.0.0.1:8333`, kalici volume `scootly-seaweedfs-data`.
- Istemci: AWSSDK.S3 (S3 API). Uygulama kodu SeaweedFS'e degil `IFileStorage` soyutlamasina bagli. Boylece bulutta S3 uyumlu herhangi bir depoya gecis yapilandirma degisikligidir (system-design.md eslesme tablosundaki "S3 uyumlu" satiri gecerli kalir).
- Depolama istege baglidir: `Storage:Enabled` varsayilan false. Kapaliyken fotograf alani gorunmez, uygulama eskisi gibi calisir.
- Fotograf kurallari: en fazla 5 MB; yalnizca JPEG ve PNG; tur, Content-Type ya da dosya adina degil icerigin imzasina (magic bytes) gore belirlenir; nesne adi sunucuda uretilir (`field-tasks/{taskId}/{guid}.jpg|png`), istemci adi hicbir yerde kullanilmaz.
- Erisim: Mvc `Photo(id)` eylemi yetki kontrolunden gecer, `Cache-Control: no-store` ile 60 saniyelik on-imzali URL'ye 302 dondurur. Imza yerelde, PublicEndpoint'e bagli ikinci bir istemciyle atilir (imza Host basligini kapsar).
- Sira: once alan kuralı (`Complete`) calisir, sonra yukleme, sonra SaveChanges. Yukleme basarisizsa degisiklik atilir ve hata doner. SaveChanges basarisizsa nesne en iyi gayretle silinir.

## Olculenler

- 13 birim testi (kural, anahtar uretimi, yerel imzalama: host/port/yol, X-Amz-Signature, X-Amz-Expires=60, gizli anahtar URL'de yok).
- Gercek SeaweedFS 4.48'e karsi canli test: AWSSDK ile bucket olusturma, yukleme, on-imzali URL ile indirme calisti.
- E2E (gercek Mvc sureci + Chromium + gecici Postgres + gercek SeaweedFS): 8/8 gecti. Operator fotografla tamamlar, "Ac" baglantisi 302 + no-store + imzali + Expires=60 verir, inen bayt dizisi yuklenenle ayni, veritabaninda durum ve anahtar beklenen bicimde. .jpg adli exe reddedilir ve gorev Assigned kalir. 5,5 MB reddedilir. Anonim kullanici giris sayfasina yonlenir.
- Depo ortam degiskenleri olmadan E2E: 3 gecer, 5 atlanir (CI yesil kalir).

## Olculmeyenler

- Kestrel sinirini (yaklasik 6 MB) asan isteklerin tarayicida nasil gorundugu.
- Imzali URL'nin 60 sn icinde baskasiyla paylasilirsa yeniden kullanilabilmesi (tasarim geregi mumkun, davranis olculmedi).
- Gelistirmede uygulama cerezinin ayni makinede farkli porttaki depoya gitmesi (cerezler port bazli yalitilmaz; etkisi olculmedi).
- Prod ortami: depo prod'a baglanmadi.

## Sonuclar ve borc

- Prod'da depo yok: `storage` profili baslatilmiyor, port kapali, nginx'te `client_max_body_size` tanimli degil (varsayilan 1 MB, buyuk fotograflar 413 alir) ve imzali URL'nin ana makine adi dis dunyadan erisilemez. Prod'a baglamak ayri is.
- Uygulama kimligi bucket olusturabilen yetkiyle calisiyor (tembel bucket olusturma). En az yetki ve onceden hazirlanmis bucket borc olarak kaydedildi.
- SaveChanges sonrasi hata + silme hatasi birlikte olursa yetim nesne kalabilir; temizleyici is yok.
- Goruntu cozumleme, kotu amacli yazilim taramasi ve EXIF temizleme yok.
- SeaweedFS saglik kontrolu master durumuna bakar, S3 uc noktasinin hazir olmasini dogrudan olcmez.