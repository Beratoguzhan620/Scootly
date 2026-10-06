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
- Blue-green gecisinde acik oturumlu bir kullanicinin oturumunun surup surmedigi (Redis anahtar halkasi paylasildigi icin beklenir, denenmedi).
- Nginx reload sirasinda ucusta olan isteklerin kesilip kesilmedigi (prova sirali, tek tek istek gonderdi).
- Yazma islemleri (POST) ve kimlik dogrulamali yollarin gecis sirasinda denenmesi: prova yalnizca anonim GET ile yapildi.
- Altyapi (postgres, redis, rabbitmq, migrator) kapaliyken green'in acilip acilmayacagi: `extends` `depends_on` tasimaz; green'in blue/altyapi ayaktayken acildigi olculdu, kapaliyken denenmedi.
- Etkin `active.conf` ile green durdurulmus bir yiginin nginx tarafindan nasil karsilandigi (cozulemeyen `-green` adlariyla reload'un reddedilip reddedilmedigi denenmedi).
- Worker'in surum gecisinde ne yaptigi: Worker cogaltilmadi ve blue-green'e dahil degil.
- Gecis sirasinda iki surumun ayni veritabanina yazmasinin sema uyumu (expand/contract disiplini kurali yazildi, bir senaryoyla denenmedi).

## 8. Blue-green dagitim (105. gun, elle, tek makinede Compose)

### 8.1 Duzen
- `deploy/nginx/nginx.conf` upstream bloklarini kendisi tasimaz, `include /etc/nginx/upstreams/active.conf;` ile alir.
- `deploy/nginx/upstreams/blue.conf` (hizmetler `mvc`, `api`) ve `green.conf` (hizmetler `mvc-green`, `api-green`) ayni yapidadir; `ip_hash` yalnizca SignalR hub upstream'indedir, tum upstream'lerde `zone` vardir.
- `active.conf` iki dosyadan birinin icerigidir ve git'te izlenir; hangi rengin canli oldugu bu dosyanin commit edilmis halinde gorulur (otomasyon yok, gecisi elle yazan kisi commit'i de elle atar).
- `deploy/docker-compose.green.yml`: `api-green` ve `mvc-green` hizmetleri, `profiles: ["green"]`, `extends` ile ana `api`/`mvc` tanimindan turer, `ports: !reset []`, `GREEN_VERSION` zorunlu (`${GREEN_VERSION:?...}`; tanimsizsa Compose hata verir), her biri 2 kopya, ortam degiskenleri ve kaynak limitleri prod override'indan elle kopyalandi (esit tutmak elle yapilir).

### 8.2 Onkosullar
- `deploy/` klasorunden calistirilir; `deploy/.env.prod` vardir.
- Blue yigini ve altyapi (postgres, redis, rabbitmq, migrator) ayakta olmalidir. Yeni surumun imaji `scootly-api:<GREEN_VERSION>` / `scootly-mvc:<GREEN_VERSION>` adlariyla `--build` ile uretilir veya hazirdir.
- Gelistirme yigini (`deploy` projesi) ile prod yigini ayni anda calismaz: sabit `container_name`ler cakisir. Prod'u calistirmadan once gelistirme yigini durdurulur (`-v` kullanilmaz, hacimler korunur).
- Prod komutlarinda proje adi `scootly-prod`'dur. Her yeni PowerShell penceresinde `$f` ve `$env:GREEN_VERSION` yeniden tanimlanmalidir (oturumlar arasinda kalmaz).

### 8.3 Prosedur
    cd deploy
    $env:GREEN_VERSION = '1.0.1'
    $f = @('-p','scootly-prod','--env-file','.env.prod','-f','docker-compose.yml','-f','docker-compose.prod.yml','-f','docker-compose.green.yml','--profile','app','--profile','green')
    docker compose @f up -d --build api-green mvc-green
    docker compose @f ps            # 4 green konteyner (healthy) olmali
    # gecis: green.conf icerigini active.conf'a yaz (BOM'suz UTF-8)
    [IO.File]::WriteAllText("$PWD\nginx\upstreams\active.conf", [IO.File]::ReadAllText("$PWD\nginx\upstreams\green.conf"), (New-Object System.Text.UTF8Encoding($false)))
    docker compose @f exec -T nginx nginx -t
    docker compose @f exec -T nginx nginx -s reload
    # geri alma: ayni komut, green.conf yerine blue.conf
    # green'i durdurma:
    docker compose @f stop api-green mvc-green

Gecisten sonra nginx gunlugundeki `upstream=` degerleri hangi kopyalarin istek aldigini gosterir.

### 8.4 Olculenler (105. gun provasi, bir kez)
- Green dort konteynerle (2 api, 2 mvc) yaklasik 10 sn icinde `healthy` oldu (migrator ve altyapi hazirdi).
- Gecis oncesi: `/Account/Login` ve `/api/v1/vehicles` icin 10'ar istek, tumu 200; trafik yalnizca blue IP'lerine (her biri 5).
- `active.conf` <- `green.conf`, `nginx -t` basarili, `reload`: 10'ar istek tumu 200; trafik yalnizca green IP'lerine (her biri 5).
- `active.conf` <- `blue.conf`, `nginx -t` basarili, `reload`: 10'ar istek tumu 200; trafik yeniden yalnizca blue IP'lerine.
- Provadan sonra green durduruldu, blue yigini saglikli kaldi; `git status` `active.conf` icin fark gostermedi (blue'ya donmustu).
- Bu prova trafik gecisini olcer. Api tarafinda `scootly-api:1.0.1` imaji o gun yeniden uretilmedi (22 saat once uretilmis, onbellekten); yani yeni api kodu denenmedi. Mvc'de DataProtection degisikligi nedeniyle 1.0.1 yeni koddur.

### 8.5 Cok kopyali calistirmada bulunan ve giderilen kusurlar (105. gun, her biri olculerek)
- Nginx icin sabit IP (10.231.0.10), kopya konteynerleriyle cakisiyordu ("Address already in use"): agin `ip_range` degeri `10.231.0.128/25` yapildi.
- Ilk dagitim olcumunde bir kopya tum istekleri aliyordu: Nginx isci surecleri kendi round-robin sayacini tutar; `zone` (mvc upstream icin `zone mvc_upstream 64k;`) eklenince dagilim dengelendi.
- Antiforgery/cookie kopyalar arasinda dogrulanamiyordu: DataProtection anahtarlari konteynere ozeldi. Mvc'de anahtar halkasi Redis'e tasindi (`SetApplicationName("Scootly.Mvc")`, `PersistKeysToStackExchangeRedis`).
- Nginx upstream adlarini baslangicta ve reload'da cozer; kopyalar yeniden olusturulunca IP'ler degisebilir ve bu durumda reload gerekir (nginx davranisi; bu belgede ayrica olculmedi).

### 8.6 Sinirlar
- Green da ayni veritabanini, Redis anahtar halkasini ve RabbitMQ'yu kullanir. Veritabani semasi degisiyorsa once genisleyen (geriye uyumlu) migration, sonra gecis, en son daraltan migration (expand/contract) uygulanmali; aksi halde geri alma eski surumu bozulmus semaya birakir.
- Worker blue-green'e dahil degildir ve cogaltilmaz.
- Redis anahtar halkasi icin kalicilik (AOF/hacim) yok: Redis sifirlanirsa yeni anahtarlar uretilir ve acik oturumlar/antiforgery belirtecleri gecersiz kalir (beklenen, olculmedi).

## 9. Ortam yonetimi (Development / Production; 105. gunde dosyalardan okundu)
Testing ortami icin ayri bir `appsettings` dosyasi yoktur; testler (`ScootlyApiFactory`, E2E) ortam degiskenleriyle yapilandirilir.

| Konu | Development | Production |
|---|---|---|
| Api Swagger | Acik (`UseScootlySwagger`) | Kapali (koddan; calisan prod'da `/swagger` istenmedi) |
| Api HSTS | Yok | Var |
| Mvc HSTS + `/Home/Error` | Yok | Var |
| Mvc oturum ve kimlik cookie'si `SecurePolicy` | `SameAsRequest` | `Always` |
| `UseHttpsRedirection` | Her iki ortamda calisir (Api ve Mvc) | Nginx `X-Forwarded-Proto` ile |
| Messaging | `appsettings.json`: Api `Enabled: true`, Mvc `false` | Ayni; base compose'ta tek bir yerde `Messaging__Enabled: "false"` (hangi servis oldugu okunmadi) |
| Rate limit (dk basina) | 60 / 10 / 30 / 600 / 300 (anonim / auth / kullanici / cihaz / webhook) | Ayni degerler; Development ve prod override'inda degisiklik gorulmedi |
| CORS | `localhost` kaynaklari (3000, 5500, 5096) | `AllowedOrigins: []` |
| Gunluk seviyesi, Seq, Otel | Debug; Seq ve Otel `localhost` | Production icin dosyada override yok (olculmedi) |
| `ForwardedHeaders:KnownProxies` | Bos | `10.231.0.10` (Nginx) |
| Gizli bilgiler | `UserSecretsId`: Api, Mvc, Worker, PaymentSimulator, DeviceSimulator | Compose ortam degiskenleri / `.env.prod` (`Jwt__*`, `ConnectionStrings__DefaultConnection`, `POSTGRES_PASSWORD`) |

Notlar:
- `ASPNETCORE_ENVIRONMENT: Production` prod override'inda payment-simulator, api ve mvc icin acikca verilir.
- user-secrets'in yalnizca Development'ta yuklenmesi ASP.NET'in varsayilan davranisidir; burada ayrica olculmedi. E2E'de Mvc `Development` ortaminda acilir, bu yuzden o ortamda bu makinenin user-secrets'i okunabilir (ADR 0040).
- Production davranislari (HSTS, Secure cookie, Swagger kapali) otomatik testle kapsanmiyor; yalnizca koddan okundu.