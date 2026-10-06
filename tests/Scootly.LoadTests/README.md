# Scootly Yuk Testleri (k6)

Senaryolar `grafana/k6` konteynerinde calisir (k6 v2.3.0 ile denendi).

| Dosya | Ne yapar | Ortam degiskenleri |
|---|---|---|
| `nearby-vehicles.js` | Anonim okuma: yarisi parametresiz (onbellekten), yarisi konum filtreli sorgu | `RATE` (istek/sn), `DURATION` |
| `start-ride.js` | Her sanal kullanici kendi hesabi ve araci ile rezerve et, baslat, bitir | `VUS`, `DURATION`, `RIDE_SECONDS`, `THINK_SECONDS` |
| `reserve-race.js` | `USERS` farkli surucu ayni araci ayni anda rezerve eder (1 basari, gerisi 409 beklenir) | `USERS` |
| `telemetry-ingest.js` | Cihaz token'i ile en fazla 500 okumalik partiler | `RATES` (virgullu, parti/sn), `STAGE_SECONDS`, `BATCH`, `DEVICE_CLIENT_SECRET`, `DEVICE_CLIENT_ID` |

Hepsinde `BASE_URL` (varsayilan `http://host.docker.internal:5016`).

## Calistirmadan once
1. **Veritabani yedegi alin.** Testler binlerce surus ve milyonlarca telemetri satiri yazar (`docs/runbook/deployment.md` bolum 3).
2. **Hiz sinirlayiciyi gecici olarak kaldirin.** Varsayilan degerlerle (ornegin Auth 10/dk IP basina) testler 429 alir.
   `RateLimiting__AnonymousPerMinute`, `__AuthPerMinute`, `__UserPerMinute`, `__DevicePerMinute` icin bir Compose override'i
   yazip (depoya eklemeyin) Api'yi yeniden olusturun; test sonunda override'siz `docker compose --profile app up -d` ile geri alin.
3. Test hesaplari `lt-user-N@scootly.test` (parola `LoadTest1234`) ilk calistirmada olusturulur ve veritabaninda kalir.
4. Telemetri senaryosu cihaz sirrini ortam degiskeninden alir; komuta yazmayin: `-e DEVICE_CLIENT_SECRET` (degersiz) ile gecirin.

## Calistirma
Ornek (PowerShell, depo kokunden):

    docker run --rm -e BASE_URL=http://host.docker.internal:5016 -e VUS=10 -e DURATION=30s `
      -v "${PWD}\tests\Scootly.LoadTests:/scripts" grafana/k6:latest run /scripts/start-ride.js

`k6-helpers.ps1` `Invoke-K6` ve `Get-Rows` fonksiyonlarini tanimlar (konteyner adlari `deploy-api-1` vb. sabit). Yuklemek icin (depo kokunden):

    . ([scriptblock]::Create([System.IO.File]::ReadAllText("$PWD\tests\Scootly.LoadTests\k6-helpers.ps1")))

## Notlar
- Esik (threshold) tanimlanmamistir; hedef sayilar olculmeden belirlenmez. `p(95)>=0` esikleri yalnizca alt metrikleri gostermek icindir.
- Ozet dosyalari `tests/Scootly.LoadTests/results/` altina yazilir; git'e girmez.
- Sonuclar: `docs/experiments/load-test-2026-10-06.md`. Karar kaydi: ADR 0039.