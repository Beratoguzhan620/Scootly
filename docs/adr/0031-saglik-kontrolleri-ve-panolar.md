# ADR 0031: Sağlık Kontrolleri ve Panolar (95. Gün)

## Durum
Kabul edildi.

## Kararlar
- **Mvc:** Api'deki `AddScootlyHealthChecks` yeniden kullanıldı; yeni kontrol sınıfı yazılmadı. Mvc'de
  `Messaging:Enabled=false` olduğundan yalnızca Postgres ve Redis kontrolleri kurulur. `/health/live`
  yalnızca süreci, `/health/ready` bağımlılıkları sorgular. Postgres kapalıyken `live` 200, `ready` 503 verir (doğrulandı).
- **Worker:** HTTP ucu olmadığı için dosya tabanlı heartbeat: servis 10 sn'de bir zaman damgası yazar,
  Compose healthcheck dosyanın son dakikada güncellendiğini kontrol eder.
  Bu yalnızca süreç canlılığıdır, bireysel işlerin sağlığı kanıtlanmaz (bkz. teknik borç).
- **Grafana:** Pano ve veri kaynağı provisioning ile gelir (elle kurulum yok). Dört altın sinyal:
  trafik, hata oranı (5xx), gecikme (p95), doygunluk (outbox kuyruğu); ek olarak sürüş hızı.
- `scootly_outbox_pending` iki serisi (Api, Worker) aynı tabloyu saydığından panoda `max` kullanılır, `sum` değil.

## Güvenlik
Grafana portu yalnızca 127.0.0.1'e açılır. Anonim erişim Viewer rolündedir. Yönetici parolası
`.env` içinde (`GRAFANA_ADMIN_PASSWORD`), git'e girmez.