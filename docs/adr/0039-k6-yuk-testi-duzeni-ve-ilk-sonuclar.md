# ADR 0039: k6 Yuk Testi Duzeni ve Ilk Sonuclar (103. Gun)

## Durum
Kabul edildi.

## Karar
- Yuk testleri k6 ile (`grafana/k6` konteyneri) `tests/Scootly.LoadTests/` altinda tutulur: okuma, surus akisi, rezervasyon yarisi ve telemetri.
  Esik (threshold) tanimlanmaz; hedef sayilar olculmeden belirlenmez. Dinamik adresli isteklere `name` etiketi verilir.
- Her sanal kullanici kendi hesabi ve kendi araci ile calisir (surucu basina tek rezervasyon/surus kuralina uygun).
- Testler sirasinda hiz sinirlayici gecici bir Compose override'i ile kapatilir; override depoya girmez.
- Her yuk testinden once veritabani yedegi alinir, sonra yedekten geri yuklenir (runbook bolum 3-4).
- Olculen degerler `docs/experiments/load-test-2026-10-06.md` dosyasinda; bu ADR yalnizca karar ve ozeti tutar.

## Olculenler (ozet)
- Rezervasyon yarisi: 50 kullanici ayni araca, 1 x 200 ve 49 x 409.
- 100 sanal kullanici, 60 sn: 3000 surus, basarisiz adim yok; ama odeme 5,44/sn ile geride kaldi (2647 surus `Pending`), sonunda hepsi `Paid`.
- Okuma 5000 istek/sn'e kadar hatasiz (p95 yaklasik 6-7 ms); tavan bulunamadi.
- Telemetri: sicak yiginda tek tuketici yaklasik 24 parti/sn (~12.000 okuma/sn) bosaltiyor; ustunde 503 + Retry-After. Kabul edilen her okuma yazildi.
- 503 yanitlari Api logunda `ERR` seviyesinde.

## Olculmeyenler
- Uretim yapilandirmasi (Nginx, 2 kopya, CPU/bellek sinirlari), hiz sinirlayici davranisi, SignalR, odeme gecikmesi (surus bazinda).
- Odeme darbogazinin nedeni (hipotez: tuketici eszamanliligi 1 ve simulator gecikmesi); dogrulama deneyi yapilmadi.
- Surus ve okuma tavanlari.

## Kisitlar
- Tek makine, tek kopya, k6 ayni makinede. Sonuclar bu ortama ozgudur.