# ADR 0032: Çok Aşamalı İmaj Derlemesi (96. Gün)

## Durum
Kabul edildi.

## Karar
Tüm uygulama imajları (Api, Worker, PaymentSimulator, Mvc) çok aşamalı derlenir. SDK aşaması derler ve
yayımlar, çalışma zamanı aşaması yalnızca yayım çıktısını taşır (`aspnet:10.0`, `USER $APP_UID`).
Önce yalnızca `csproj` dosyaları kopyalanıp `restore` çalıştırılır, böylece kaynak kod değişse de restore
katmanı önbellekten gelir.

## Ölçüm
Aynı kaynak, aynı makine, tek ölçüm (`docker images` Size sütunu):

| İmaj | Boyut |
|---|---|
| Mvc, çok aşamalı (`deploy-mvc`) | 379 MB |
| Mvc, tek aşamalı SDK tabanlı (deney, `docs/experiments/mvc-single-stage`) | 1,61 GB |

Tek aşamalı imaj yaklaşık 4,2 kat büyük. Katman dökümü alınmadı.

## Gerekçe
- Tek aşamalı imaj SDK taban imajını ve `src/` kaynak kodunu çalışma zamanına taşır; çok aşamalıda
  bunlar derleme aşamasında kalır.
- Derleme araçları ve kaynak kod üretim imajında bulunmaz.
- `.dockerignore` `bin/`, `obj/`, `.env` dosyalarını ve `docs/`, `tests/` klasörlerini bağlam dışında bırakır.

## Mvc'ye özgü notlar
- Mvc imajı Api projesini içermez (Mvc, Api'ye bağlı değildir).
- Healthcheck `CMD-SHELL` ile çalışmaz: imajdaki `sh` dash'tir ve `/dev/tcp` desteklemez. Compose'ta
  `bash` açıkça çağrılır. Başarılı yol `(healthy)` ile görüldü; kontrol mantığı betikle iki yönde
  doğrulandı (8080 → çıkış 0, 9999 → çıkış 2). Docker'ın bu çıkış kodunu `unhealthy` olarak
  işaretlemesi gerçek bir arıza üretilerek denenmedi.