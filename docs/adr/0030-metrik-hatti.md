# ADR 0030: Metrik Hattı (94. Gün)

## Durum
Kabul edildi.

## Karar
Uygulamalar metrikleri OTLP ile OpenTelemetry Collector'a iter; Collector bir Prometheus ucu (8889)
açar, Prometheus oradan okur. Uygulamaların kendi üzerinde `/metrics` ucu yoktur.

## Gerekçe
- Dokümanın "`/metrics`'i dışarı açma" kuralı yapısal olarak sağlanır: kapatılacak bir uç yok.
- İzler ve metrikler aynı Collector yolunu kullanır; tek yapılandırma noktası (`Otel:Endpoint`).
- Collector'ın 8889 portu host'a yayımlanmaz, yalnızca Compose ağı içinde Prometheus erişir.

## Metrik tasarımı
- `Meter("Scootly")`: `scootly.rides.started`, `scootly.rides.completed` (sayaç),
  `scootly.outbox.pending` (gauge).
- Metrikler Application katmanına konmadı; sayaçlar Api'de (`RidesController`), gauge Infrastructure'da.
- Etiketlere kullanıcı/araç kimliği konmaz (kardinalite).
- Gauge geri çağrısı eşzamanlı olduğu için veritabanı sorgusu `OutboxMetricsCollector` arka plan
  servisinde yapılır; gauge bellekteki son değeri raporlar.
- İstek süresi için ayrı metrik yazılmadı: ASP.NET Core enstrümantasyonu `http.server.request.duration`
  histogramını kendisi üretir.