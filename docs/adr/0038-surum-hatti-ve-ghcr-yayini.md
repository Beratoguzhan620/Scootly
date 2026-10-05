# ADR 0038: Surum Hatti ve GHCR Yayini (102. Gun)

## Durum
Kabul edildi.

## Karar
`v*` etiketi `.github/workflows/release.yml`'yi tetikler: etiket anlamsal surum olmali (`vMAJOR.MINOR.PATCH[-onsurum]`);
etiketli commit icin `ci.yml`'in basarili bir calismasi bulunmali; `CHANGELOG.md`'de `## [<surum>]` bolumu bulunmali.
Sonra bes imaj (`scootly-api`, `scootly-migrator`, `scootly-worker`, `scootly-payment-simulator`, `scootly-mvc`)
`ghcr.io/beratoguzhan620/<ad>:<surum>` olarak yayinlanir ve GitHub Release olusturulur (adinda `-` olan surumler pre-release).
Imajlar yalnizca surum numarasiyla etiketlenir (`latest` yok). Ucuncu taraf eylem kullanilmaz: `docker build/push` ve `gh`.
Imajlara `org.opencontainers.image.source/revision/version` etiketleri eklenir. Ilk yayin `main`'e dokunmadan
`feature/week21-cicd-testing` dalindan `v0.1.0-rc.1` ile yapildi. `berat`, `main`'in 107 commit ilerisinde; `main`'deki 3 commit
birlestirme commit'leridir ve `git diff --stat berat...main` bostur.

## Olculenler
- `actionlint`: 2 dosyada 0 hata, cikis kodu 0.
- `v0.1.0-rc.1` yayini yesil bitti (commit be423d4). CI kapisi `gh run list --commit` ile 1 basarili calisma buldu.
  CHANGELOG bolumu Actions'ta dogru cikarildi. Ozet tabloda bes imajin gercek digest'i var.
- `scootly-api:0.1.0-rc.1` cekildi: digest Summary tablosuyla ayni, etiketler beklenen (revision = etiketlenen commit),
  kullanici `app` (uid 1654), `/app/Scootly.Api.dll` mevcut.
- Bes paket de bos Docker yapilandirmasiyla `manifest inspect` ile erisilebildi (herkese acik). Gorunurluk ayari degistirilmedi;
  paketlerin neden zaten acik oldugu bilinmiyor.
- GitHub Release sayfasinda "Pre-release" rozeti gorundu; paket sayfasinda surum etiketi ve bagli depo gorundu.
- `awk` ile CHANGELOG bolumu cikarma ornek dosyada denendi: var olan surum dogru cikti, olmayan surum bos dondu.

## Olculmeyenler
- Diger dort imajin icerigi (yalnizca manifestlerine erisildi). Yayinlanan imajlarla uygulamanin bir veritabaniyla acilmasi.
- Hatali yollar: kirmizi CI ile etiket, CHANGELOG bolumu olmayan surum, gecersiz etiket adi GitHub'da denenmedi.
- Etiket silme veya yeniden etiketleme davranisi. GHCR'da surum silme/saklama politikasi.
- Compose'un GHCR imajlarini kullanmasi (`image:` satirlari yerel adlara isaret eder). Adim sureleri kaydedilmedi.
- `gh release create` icin ayri bir deneme yok; yalnizca sonuc (Release sayfasi) gozlendi.

## Kisitlar
- `-s` kontrolu yalnizca bos satirlardan olusan bir CHANGELOG bolumunu da gecirir.
- `release.yml` `actions: read`, `contents: write`, `packages: write` yetkisi ister; yetki degisirse is akisi kirilir.
- Etiket `be423d4`'e isaret eder ve bu commit PR ile `berat`'a girer.