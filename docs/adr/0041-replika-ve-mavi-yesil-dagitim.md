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

## Olculmeyenler
- Oturum surekliligi, ucusta istek davranisi ve yazma isteklerinin gecis sirasinda calismasi.
- Api'de yeni kod: provada api imaji yeniden uretilmedi (eski 22 saatlik imaj).
- Calisan blue api imajinin kimligi (`sha256:0a78...`), `scootly-api:1.0.0` etiketinin kimligiyle (`ea4a6dd7...`) eslesmiyor; Docker Desktop containerd deposu kullaniyor, neden belirlenmedi. Etikete dayali geri alma api icin dogrulanmadi.
- Redis kesintisinde Mvc davranisi; anahtar halkasi kalici degil.
- Altyapi kapaliyken green'in acilmasi (`extends` `depends_on` tasimaz).

## Kisitlar
- `docker-compose.green.yml` prod override'indaki ortam degiskenlerini ve kaynak limitlerini elle kopyalar; ikisi birbirinden uzaklasabilir.
- Gelistirme ve prod yiginlari sabit `container_name` nedeniyle ayni anda calismaz.
- Veritabani semasi icin expand/contract disiplini kuraldir, otomatik denetlenmez.