# ADR 0033: Üretime Hazır İmaj ve Kapanış Davranışı (97. Gün)

## Karar
- **Healthcheck:** ASP.NET Core konteynerleri (Api, Mvc, PaymentSimulator) için ortak `bash` + `/dev/tcp`
  kontrolü (YAML çapasıyla tek tanım); Worker için dosya tabanlı heartbeat. Seçilmeyenler: imaja `curl`
  kurmak (boyut artar), uygulamaya `--healthcheck` modu eklemek (kod değişikliği).
  Bağımlılık: imajdaki `bash`, `head`, `grep`.
- **Sürüm etiketi:** `scootly-<servis>:${SCOOTLY_VERSION:-1.0.0}`; 102. günde release hattı değişkeni ezer.
- **Api → payment-simulator bağımlılığı** `service_healthy` oldu; `up` sırasında Api, simülatör sağlıklı olana kadar bekler.

## Kapanış davranışı (ölçüldü)
`docker stop` işlenmekte olan bir ödeme mesajı varken 0,46 sn'de, `ExitCode=0` ile tamamlandı; varsayılan 10 sn'lik
SIGKILL sınırına yaklaşılmadı, bu yüzden `stop_grace_period` değiştirilmedi. Tüketici mesajı `requeue: true` ile
bıraktı (kapanış log satırı eklendi ve tetiklendi). Docker'ın sıfırdan farklı çıkış kodunu `unhealthy` saydığı
boş bir aspnet konteynerinde ayrıca gösterildi.
Uçtan uca doğrulama: simülatör dondurulurken tamamlanan sürüşün mesajı kapanışta `requeue` edildi,
yeniden başlayan Api aynı sürüşün ödemesini aldı (`Paid`, `PaymentAttempts=1`). Mesaj kaybolmadı.