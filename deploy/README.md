# deploy/ — yerel geliştirme altyapısı

## İlk kurulum

1. `.env.example` dosyasını `.env` adıyla kopyala.
2. `.env` içindeki `POSTGRES_PASSWORD` değerini **kendi ürettiğin** uzun ve rastgele
   bir parolayla değiştir. Bu dosya `.gitignore`'dadır, depoya hiç gitmez.

Parolayı elle uydurma; üret. Git Bash'te:

```bash
openssl rand -base64 32
```

3. Veritabanını başlat:

```bash
cd deploy
docker compose up -d
docker compose ps        # health durumu "healthy" olmalı
```

4. Aynı parolayı API'nin user-secrets'ına da gir (bkz. kök `README.md`).
   Parola iki yerde durur: `deploy/.env` ve user-secrets. İkisi de Git'e uğramaz.

## Neden `.env` ve neden bu ayarlar

`docker-compose.yml` içindeki her güvenlik ayarının gerekçesi dosyanın kendi
yorumlarında yazılı. Özetle:

| Ayar | Ne engelliyor |
|---|---|
| `127.0.0.1:5432:5432` | Veritabanının yerel ağa açılmasını |
| `${POSTGRES_PASSWORD:?...}` | Parolanın dosyada açık metin durmasını; eksikse konteyner hiç başlamaz |
| `postgres:16-alpine` (sabit sürüm) | Ana sürümün fark edilmeden değişmesini |
| `--auth-host=scram-sha-256` | Eski istemcinin md5'e düşürmesini |
| `no-new-privileges:true` | Konteyner içinde setuid ile yetki yükseltmeyi |
| `healthcheck` | "Ayakta" ile "bağlantı kabul ediyor" karışıklığını |

## Docker çalışmayan ortamlar

Apple Silicon üzerinde Parallels ile çalışan bir Windows sanal makinesinde Docker
Desktop kurulamaz — iç içe sanallaştırma (nested virtualization) M3 öncesi Apple
Silicon'da yok. Bu durumda seçenekler:

- Konteyneri **macOS tarafında** çalıştır ve Windows'tan Parallels'in paylaşılan ağ
  adresi üzerinden bağlan. Bunun için portu `10.211.55.2:5432` olarak açan bir
  `docker-compose.override.yml` yazılır (bu dosya da `.gitignore`'dadır).
- Ya da geliştirme için yönetilen (hosted) bir PostgreSQL kullan ve bağlantı
  dizesini user-secrets'a gir.

Her iki durumda da `deploy/docker-compose.yml` ekibin geri kalanı ve CI için
geçerli kalır.
