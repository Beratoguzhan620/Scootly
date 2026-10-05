# ADR 0034: Üretim Compose Yapılandırması (98. Gün)

## Durum
Kabul edildi.

## Karar
`deploy/docker-compose.prod.yml`, temel dosyanın üzerine uygulanan bir override'dır. Eklenenler:
`restart: unless-stopped`, servis başına `deploy.resources.limits`, `json-file` log rotasyonu
(10 MB x 3 dosya), `ASPNETCORE_ENVIRONMENT=Production` ve `AllowedHosts`.
Postgres, Redis, RabbitMQ ve PaymentSimulator portları `!reset` ile ana makineye kapatıldı;
Api ve Mvc portları Nginx'e (99. gün) kadar 127.0.0.1'de açık kalır.
Sırlar `deploy/.env.prod` içindedir (git'e girmez); şablon `.env.prod.example`.
Üretim ayrı proje adıyla (`-p scootly-prod`) çalışır ve kendi PostgreSQL hacmine sahiptir.

## AllowedHosts
`localhost;api`. `api` gereklidir: PaymentSimulator webhook'u `Host: api` ile gönderir.
Doğrulandı: `localhost` → 200, `api:8080` → 200, `evil.example` → 400.

## Ölçümler (boşta, 90 sn sonra)
Tüm servislerin bellek kullanımı sınırın %20'sinin altındaydı (Api 87/512 MiB, Mvc 37/384, Worker 74/384,
RabbitMQ 137/768, Postgres 55/1024, Redis 8/256, PaymentSimulator 21/192). Sınırlar ilk tahmindir;
yük altında ölçülmedi, bu yüzden küçültülmedi.

## Kısıtlar
- Temel dosyada sabit `container_name` olduğundan geliştirme ve üretim yığınları aynı anda çalışamaz.
- `unhealthy` konteyner kendiliğinden yeniden başlatılmaz; `restart` yalnızca süreç çıkışında devreye girer.