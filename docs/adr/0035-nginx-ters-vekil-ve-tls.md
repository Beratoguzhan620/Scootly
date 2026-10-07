# ADR 0035: Nginx Ters Vekil ve TLS (99. Gun)

## Durum
Kabul edildi.

## Karar
Uretimde tek giris Nginx'tir (127.0.0.1:8443 -> 443, TLS Nginx'te sonlanir). `/` Mvc'ye, `/api/` ve `/hubs/` Api'ye gider.
Api 2 kopya calisir. `/api/` icin round-robin, `/hubs/` icin `ip_hash` kullanilir. Nginx'e sabit IP verilir
(10.231.0.10) ve Api/Mvc `ForwardedHeaders:KnownProxies` ile yalnizca onun X-Forwarded-* basliklarina guvenir.
Mvc'ye `UseForwardedHeaders` eklendi ve CSP `connect-src` artik `ApiBaseUrl`'den uretilir (sabit localhost:5016 yoktu).
`ApiBaseUrl` uretimde https://localhost:8443 oldugu icin tarayici tek origin'e baglanir (CORS gerekmez).
Sertifika kendinden imzali, `deploy/nginx/certs` altinda; `.crt`/`.key` git'e girmez.

## Olculenler
- Giris https uzerinden calisti; `Secure` cookie kabul edildi (96. gunden kalan http uzerinden giris sorunu kapandi).
- `upstream` blogunda `zone` yokken 20 isci sureci her biri kendi sayacini tuttu ve 20 istegin 19'u ayni kopyaya gitti.
  `zone` eklenince dagilim 10/9 ve sirayla donumlu (.8, .7, .8, .7...) oldu.
- `/hubs/` blogunda kendi `proxy_set_header` olunca ust duzeydeki `Host` basligi miras kalmadi; negotiate
  tarayicidan 400 (Invalid Hostname) aldi. Basliklar bloga yeniden yazilinca ayni tarayici istegi 200 aldi.
- Harita rozeti "Bagli", WebSocket 101 ile yukseltildi (Nginx logunda `GET /hubs/fleet 101`).
- Nginx erisim logunda `access_token` yok (`$uri` sorgu dizesini icermez).

## Olculmeyenler
- `ip_hash`: Docker Desktop'ta tum istekler tek kaynak IP'den gorundugu icin hub dagilimi gosterilemedi.
- Iki Api kopyasi arasinda canli arac guncellemesi; yalnizca baglanti kuruldu.
- Api kapsayici loglarinda `access_token` yok, ancak uretimde `Microsoft.AspNetCore` gunlugu Information olmadigi
  icin bu bir olcum degil log seviyesinin sonucudur.
- X-Forwarded-* guveninin (KnownProxies) calistigi ayrica olculmedi.

## Kisitlar
- Kendinden imzali sertifika tarayicida uyari verir; uretimde gercek bir otorite gerekir.
- Nginx upstream adreslerini acilista cozer; bir kopya yeniden baslayip IP'si degisirse Nginx yeniden baslatilmali.
- Hiz sinirlayici kopya basina bellekte tutulur; sinir iki kopya arasinda bolunur.
- Nginx'te bir `location` kendi `proxy_set_header`'ini tanimlarsa ust duzeydekiler o location'a miras kalmaz.