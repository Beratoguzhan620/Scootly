# ADR 0041: Cok Kopyali Calistirma ve Elle Blue-Green Dagitim (105. Gun)

## Durum
Kabul edildi.

## Karar
- Prod yiginda `api` ve `mvc` ikiser kopya (`deploy.replicas: 2`) calisir; Nginx upstream'leri `zone` ile paylasimli sayac kullanir, SignalR hub upstream'i `ip_hash` ile sabitlenir.
- Nginx'in sabit IP'si icin agda `ip_range: 10.231.0.128/25` tanimlanir; kopya konteynerleri bu aralikta IP alir.
- Mvc'de DataProtection anahtar halkasi Redis'te tutulur (`PersistKeysToStackExchangeRedis`, uygulama adi `Scootly.Mvc`); boylece kopyalar arasi antiforgery ve cookie dogrulamasi calisir.
- Blue-green: `deploy/nginx/upstreams/{blue,green,active}.conf`, `nginx.conf`'ta `include`; yesil yigin `docker-compose.green.yml` ile (`profiles: ["green"]`, `GREEN_VERSION` zorunlu) kaldirilir. Gecis ve geri alma `active.conf` icerigini degistirip `nginx -t` + `nginx -s reload` ile yapilir. Otomasyon yok; adimlar `docs/runbook/deployment.md` bolum 8'de.
- Green yigin altyapiyi (postgres, redis, rabbitmq, migrator) ve Worker'i paylasir; Worker cogaltilmaz.

## Olculenler
- Cok kopyali calistirmada uc kusur bulundu ve giderilip dogrulandi: sabit IP cakismasi, tek kopyaya giden trafik (`zone` eksigi), kopyalar arasi antiforgery/cookie hatasi (DataProtection).
- `include` duzeninde `nginx -t` basarili; trafik 2 Mvc ve 2 Api kopyasi arasinda esit dagildi (5/5).
- Green yigini (4 konteyner) yaklasik 10 sn'de `healthy` oldu; gecis ve geri alma sirasinda 20/20 istek 200 dondu ve gunlukte yalnizca beklenen renk IP'leri goruldu.
- `extends` ve `!reset` calisma zamaninda beklendigi gibi calisti.
- CI (1ab438d): `build-and-test`, `docker-images`, `e2e` yesil; kapsam raporu satir %73,7, dal %60,6, Worker %0.

## Olculenler (ikinci tur)
- Antiforgery cookie+token blue->green gecisinde gecerli kaldi (200 x4); bozuk token 400 x2 (kontrol).
- Reload altinda yuk: 4 reload boyunca 400 sirali GET'in 400'u 200; nginx'te `[error]` satiri 0.
- Hatali reload (green durdurulmus, `active.conf`=green): `nginx -t` ve `reload` "host not found in upstream" ile reddedildi; eski yapilandirma calismaya devam etti (10/10 istek 200, blue).
- Green'in `depends_on` degerlerini `extends` ile miras aldigi `docker compose config` ile gorundu (onceki "tasimaz" varsayimi yanlisti).
- `Scootly.Api.dll` SHA-256'si 1.0.0, 1.0.1 ve calisan blue konteynerde ayni.

## Olculenler (odeme tuketicisi deneyleri)
- Deney A (onay, prod yigini, Worker durdurulmus): 30 `Pending` surus, her biri icin ayni `RideCompleted` mesaji 2 kez es zamanli yayinlandi (`rabbitmqadmin`, MessageId'siz): 30/30 `Paid`, `PaymentAttempts = 1`, Api gunlugunde 30 farkli surus icin 30 "Odeme alindi", "ucretlendirilemedi" ve [WRN]/[ERR] 0, kuyruk ve DLQ bos.
- Deney B (ret; simulator gecici olarak Development'ta, ret orani %100, Worker durdurulmus): ayni duzen: 30/30 `Pending`, `PaymentAttempts = 2`, 60 "Odeme reddedildi" satiri (her surus icin 2), "ucretlendirilemedi" ve [WRN]/[ERR] 0 (devre kesici 60 ret sirasinda hata uretmedi), kuyruk ve DLQ bos. Yani ayni mesajin cift teslimi, reddedilmis bir odemede 5 deneme hakkindan 2'sini tuketiyor (ikinci kopya `attempt-2` anahtariyla saglayiciya yeni istek gonderir); `Paid` durumunda bu olmaz.
- Her iki deneyde de deney satirlari silindi, Worker yeniden baslatildi, simulator Production'a geri alindi.

## Olculmeyenler
- Giris (kimlik) cookie'sinin gecis sirasinda surmesi, uzun omurlu baglantilar (SignalR/WebSocket) ve yazma isteklerinin gecis sirasinda calismasi. Antiforgery cookie+token blue->green gecisinde gecerli kaldi (olculdu).
- Api'de yeni kod: provada api imaji yeniden uretilmedi (22 saatlik eski imaj); `Scootly.Api.dll` SHA-256'si 1.0.0, 1.0.1 ve calisan blue konteynerde ayni.
- Calisan blue api imajinin kimligi (`sha256:0a78...`) `scootly-api:1.0.0` etiketinin kimligiyle (`ea4a6dd7...`) eslesmiyor; tam kimlikle `docker image inspect` "No such image" verdi (Docker Desktop containerd deposu); nedeni belirlenmedi. `Scootly.Api.dll` SHA-256'si ise ayni; bu tek dosyaya dayanir, tum imaj karsilastirilmadi.
- Redis kesintisinde Mvc davranisi; anahtar halkasi kalici degil.
- Altyapi kapaliyken green'in acilmasi: `depends_on`'in `extends` ile miras alindigi `compose config` ile gorundu, bu yuzden `up`'in altyapiyi baslatip beklemesi beklenir; denenmedi.

## Kisitlar
- `docker-compose.green.yml` prod override'indaki ortam degiskenlerini ve kaynak limitlerini elle kopyalar; ikisi birbirinden uzaklasabilir.
- Gelistirme ve prod yiginlari sabit `container_name` nedeniyle ayni anda calismaz.
- Veritabani semasi icin expand/contract disiplini kuraldir, otomatik denetlenmez.