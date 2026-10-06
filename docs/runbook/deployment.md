# Dagitim ve Geri Alma Runbook'u

Bu belge yalnizca denenen adimlari "Denendi" olarak isaretler. Denenmeyenler "Denenmedi" bolumunde ayrica listelenir.

## 1. Surumler
- Surum etiketi `vMAJOR.MINOR.PATCH[-onsurum]` bicimindedir (ornek: `v0.1.0-rc.1`). Etiket atilinca `Release` is akisi calisir
  (`.github/workflows/release.yml`): ayni commit icin `ci.yml`'in basarili bir calismasi yoksa durur; CHANGELOG.md'de
  `## [<surum>]` bolumu yoksa durur; sonra bes imaji `ghcr.io/beratoguzhan620/<ad>:<surum>` olarak yayinlar ve GitHub Release olusturur.
- Imajlar: `scootly-api`, `scootly-migrator`, `scootly-worker`, `scootly-payment-simulator`, `scootly-mvc`.
- Etiket yalnizca surum numarasidir; `latest` yayinlanmaz, geri alma her zaman tam bir surumu adlandirir.
- Onsurum (adinda `-` olan) GitHub'da "pre-release" olarak isaretlenir.
- Is akisinin sozdizimi `actionlint` ile denetlendi (0 hata). `v0.1.0-rc.1` ile ilk yayin yapildi (commit be423d4): CI kapisi,
  CHANGELOG bolumu cikarma, bes imajin GHCR'a itilmesi ve pre-release olusturma calisti. `scootly-api` imaji cekildi; digest
  Summary tablosuyla ve etiketler (revision, source, version) etiketlenen commit ile eslesti, kullanici uid=1654. Bes paketin
  tamami bos Docker yapilandirmasiyla (kayitli giris olmadan) `manifest inspect` ile erisilebildi, yani herkese aciktir.

## 2. Mevcut dagitim (yerel Compose)
- Uretim komutu (deploy/ klasorunden):
  `docker compose -p scootly-prod --env-file .env.prod -f docker-compose.yml -f docker-compose.prod.yml --profile app up -d --build`
- Compose imaj adlari yerel ve surum degiskenlidir: `scootly-api:${SCOOTLY_VERSION:-1.0.0}` gibi, `build:` bloklariyla.
  Ayni `SCOOTLY_VERSION` ile yeniden derleyince etiket ustune yazilir (derleme ciktisinda `naming to ...scootly-api:1.0.0` goruldu);
  bu yuzden yerel derlemeyle "onceki surume donmek" icin eski imajin ayri bir etiketle saklanmis olmasi gerekir.
- `migrator` servisi her `up`'ta calisir ve cikis yapar (`Exited` gozlendi); EF Core migration paketi bekleyen migration'lari uygular.

## 3. Veritabani yedegi (Denendi)
Gelistirme kapsayicisinda:docker exec scootly-postgres pg_dump -U postgres -d scootly -Fc -f /tmp/yedek.dump
docker cp scootly-postgres:/tmp/yedek.dump .Uretimde kullanici ve veritabani adlari `.env.prod` degerleriyle ayni olmalidir (kontrol edilmedi).
Her migration'dan ONCE yedek alin.

## 4. Yedekten geri yukleme provasi (Denendi, ayri veritabanina)docker exec scootly-postgres createdb -U postgres scootly_restore_test
docker exec scootly-postgres pg_restore -U postgres -d scootly_restore_test /tmp/yedek.dump
docker exec scootly-postgres dropdb -U postgres scootly_restore_testSonuc: yedek 1.490.087 bayt; geri yuklenen kopyada 500 arac, 22 surus, 3 kullanici, 77 outbox kaydi ve son migration
`AddOutboxTraceParent` asil veritabaniyla ayniydi, `pg_restore` hata yazmadi. Bu yalnizca yedegin ayri bir veritabanina
geri yuklenebildigini gosterir.

## 5. Geri alma secenekleri
1. **Yedekten geri yukleme (birincil yol).** Migration oncesi alinan yedek; asil veritabaninin uzerine yukleme DENENMEDI.
   Uygulama kapsayicilari (api, worker, mvc) durdurulmadan yapilmamalidir.
2. **Onceki surume donmek (kod).** `SCOOTLY_VERSION`'i onceki surume ayarlamak yalnizca o etiketli imaj mevcutsa isler.
   Yeni sema ile eski kodun uyumlulugu DENENMEDI; sema degistiyse yedek gerekir.
3. **`Down` migration'i.** Kayiplidir (bkz. 6). Yalnizca kopya bir veritabaninda kullanin; hic calistirilmadi.

## 6. `HardenDomainPaymentsAndMessaging.Down` kapsami (koddan okundu, calistirilmadi)
| Islem | Etki |
|---|---|
| Completed + odeme Pending/Failed + deneme>0 surusler | Durum `PaymentPending` olur |
| Completed + odeme Pending + deneme=0 surusler | `Fare` NULL olur (ucret kaybi) |
| `ProcessedMessages`'ta ayni `MessageId` icin birden cok kayit | Biri birakilir, digerleri SILINIR |
| 10 indeks ve `PK_ProcessedMessages` | Kaldirilir; birincil anahtar tek sutuna (`MessageId`) doner |
| `AspNetRoles`'tan 3 tohum rol satiri | Silinir (kullanici atamalarina etkisi gorulmedi) |
| 11 sutun: `Vehicles.LastTelemetryAt/ReservedBy`, `Rides.LastPaymentAttemptAt/LastPaymentError/PaidAt/PaymentAttempts/PaymentStatus/xmin`, `ProcessedMessages.Consumer`, `OutboxMessages.Attempts/LastError` | DUSURULUR (veri kaybi) |
| `Rides.StartLatitude/StartLongitude` | NOT NULL olur, `defaultValue: 0.0` |
| `Rides.Fare` | `numeric(10,2)` -> `numeric` |

Belirsiz noktalar: NULL baslangic koordinatlarinin `(0,0)`'a donusup donusmedigi, rol silmenin kullanici-rol atamalarini
cascade ile silip silmedigi, `xmin` sutununu dusurmenin Npgsql'de ne yaptigi. Uretilen SQL incelenmedi.

## 7. Denenmedi
- Yedegin asil veritabaninin uzerine geri yuklenmesi ve uygulamanin geri yuklenen veritabaniyla acilmasi.
- `Down` migration'inin calistirilmasi.
- Yayinlanan imajlarla uygulamanin bir veritabaniyla acilmasi. Yalnizca `scootly-api` imaji cekilip incelendi; diger dort imajin
  icerigi incelenmedi (yalnizca manifestlerine erisilebildigi dogrulandi).
- Compose'un GHCR imajlarini kullanmasi: `image:` satirlari su an yerel adlara isaret eder.
- 103. gunde yuk testi oncesi yedek `pg_dump -Fc` ile konteyner icinde alinip `docker cp` ile ana makineye kopyalandi (1.490.087 bayt), depo disinda saklandi.
  Yedekten geri yukleme yine yalnizca ayri bir veritabanina denendi (102. gun); asil veritabaninin uzerine yukleme bu yedekle de yapilmadi.
  Yuk testi verisi gelistirme veritabaninda birakildi (bkz. technical-debt.md, 103. gun).