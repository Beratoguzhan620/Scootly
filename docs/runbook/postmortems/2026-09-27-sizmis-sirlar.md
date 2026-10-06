# Postmortem: Public depoda sizmis ve hala kullanimda olan sirlar (2026-09-27)

## Durum ve seviye
- Durum: Kapandi (sirlar yenilendi); aksiyonlardan biri acik.
- Seviye: SEV1 (sir sizintisi; geriye donuk siniflandirma).
- Sure: sizinti en erken 2026-08-20 (ilk commit) ile 2026-09-27 arasi; en fazla 38 gun. Kesin baslangic bilinmiyor.
- Kaynaklar: plan belgesi bolum 2.2-2.3, ADR 0021, teknik borc listesi, `git log`, `.github/workflows/ci.yml`. Ek beyan (Berat): depo ilk bastan beri public; kotu kullanima dair iz yok.

## Ozet
27.09.2026'daki kapsamli teknik incelemede JWT imza anahtari, cihaz sirri, webhook sirri ile PostgreSQL ve RabbitMQ parolalarinin Git gecmisinde durdugu, deponun public oldugu ve ayni degerlerin hala kullanildigi bulundu. Ayni gun degerlerin tamami yenilendi, Redis'e parola eklendi, sirlar koddan cikarildi ve ayarlar Options + `ValidateOnStart` ile dogrulanir hale getirildi. Kotu kullanima dair iz bulunmadi.

## Etki
- Okunabilirlik: public depoda herkes eski degerleri okuyabilirdi (ADR 0021).
- Olasi etki (ADR 0021'e gore): JWT anahtariyla herkes `FleetManager` rolunde ya da baska bir kullanici adina token uretebilirdi; cihaz sirri DeviceSimulator'in, webhook sirri Infrastructure'in kaynak kodundaydi. Sahte telemetri ve sahte odeme bildirimi imkani bundan cikarilan olasi sonuclardir.
- Gerceklesen etki: kotu kullanima dair iz yok (Berat'in beyani). Iz arama yontemi ve kapsami kayitli degil; bu nedenle "kullanilmadi" degil "iz bulunmadi" olarak okunmali.
- Erisim yuzeyi: altyapi portlari yalnizca 127.0.0.1'e aciktir (plan belgesi, durum tablosu); disaridan erisilen bir dagitim belgelerde yok. Bu yuzeyi daraltir ama kanitlanmis degildir.

## Zaman cizelgesi

| Tarih | Olay | Kaynak |
|---|---|---|
| 2026-08-20 | Ilk commit `6ae5250`; ilk `appsettings*.json` eklenmesi `b3142ed` | `git log` |
| 2026-08-20 ve sonrasi | Depo ilk bastan beri public (beyan); sirlarin her birinin ilk girdigi commit belirlenmedi | beyan |
| 2026-09-27 (saat bilinmiyor) | Kapsamli teknik inceleme: sizmis ve kullanimdaki sirlar bulundu | plan 2.2 |
| 2026-09-27 | JWT, cihaz, webhook, PostgreSQL, RabbitMQ sirlari yenilendi; Redis'e parola eklendi | plan 2.2, ADR 0021 |
| 2026-09-27 | Sirlar koddan cikarildi; Options + `ValidateOnStart`; `deploy/.env` + `.env.example` | ADR 0021; ilgili commit'ler (plan 2.3 basliklarindan): `f332cdc`, `262538e`, `76db68d`, `f4eda08` |
| 2026-09-27 | ADR 0021 kabul edildi; dokuman guncellemesi `3ae5465` | plan |
| 2026-10-06 | CI'da paket zafiyet taramasi var, secret taramasi yok (okundu) | `ci.yml` satir 83-86 |

## Tespit
Teknik inceleme (27.09.2026). Bulguyu kimin ve nasil fark ettigi kayitli degil. Otomatik tespit yoktu: CI'da secret taramasi yok.

## Kok neden
1. Sirlar kaynak kodda ve appsettings'te tutuluyordu (ADR 0021).
2. Depo public'ti.
3. Sirlar rotate edilmemisti; sizan degerler kullanimda kaldi.
4. Git gecmisi temizlenmedi; sonradan da yeniden yazilmadi (paylasilan dallari bozar, ADR 0021).
5. Otomatik tarama yoktu: CI yalnizca `dotnet list package --vulnerable` calistirir (cikarim: bu yuzden tespit insan incelemesine kaldi).

## Neler iyi gitti / Neler kotu gitti / Sans
- Iyi: sirlarin tamami ayni gun yenilendi; ayarlar `ValidateOnStart` ile dogrulanir (eksik ya da kisa anahtar uygulamayi hic acmaz, ADR 0021).
- Kotu: sirlar depo ilk olustugundan beri public gecmisteydi; tespit insan incelemesine bagliydi; rotasyon adimlari kayda gecmedi.
- Sans: kotu kullanima dair iz bulunmadi; altyapi portlari yalnizca 127.0.0.1'e aciktir.

## Aksiyonlar

| Aksiyon | Sahip | Tarih | Durum |
|---|---|---|---|
| Sirlari yenile (JWT, cihaz, webhook, PostgreSQL, RabbitMQ; Redis'e parola) | belirtilmedi | 2026-09-27 | Tamam |
| Sirlari koddan cikar; user-secrets / `deploy/.env` / ortam degiskeni | belirtilmedi | 2026-09-27 | Tamam |
| Options + `ValidateOnStart`, anahtarlar en az 32 karakter | belirtilmedi | 2026-09-27 | Tamam |
| CI'da paket zafiyet taramasi | belirtilmedi | belirlenmedi | Tamam (`ci.yml` 83-86; eklenme tarihi belirlenmedi) |
| Secret taramasi: GitHub secret scanning + push protection ya da gitleaks, CI'a eklenmesi | belirlenmedi | belirlenmedi | Acik |
| Her sirrin Git gecmisinde ilk gectigi commit'in belirlenmesi (`git log -S`, degerleri yazdirmadan) | belirlenmedi | belirlenmedi | Acik |
| Git gecmisinin temizlenmesi (`git filter-repo`) | - | - | Bilincli yapilmadi (ADR 0021): rotasyon yeterli ve zorunlu |
| Rotasyon prosedurunun denenip kaydedilmesi (`incident-response.md` bolum 6) | belirlenmedi | belirlenmedi | Acik |

## Olculenler / Olculmeyenler
- Olculen ya da dogrudan okunan: sirlarin 27.09.2026'da yenilendigi (plan, ADR 0021); sizinti kapsaminin ADR 0021'deki listesi; CI'da yalnizca paket zafiyet taramasi oldugu (`ci.yml`); ilk commit tarihi ve `appsettings*.json` ilk eklenme tarihi (`git log`).
- Olculmeyen / bilinmeyen: sizinti suresinin kesin baslangici ve hangi sirrin hangi commit'te girdigi; kotu kullanim aramasinin yontemi ve kapsami; bulguyu fark eden kisi ve olayin saati; rotasyon adimlari (kayitli degil); iz bulunmamasinin kullanilmadigi anlamina geldigi (kanitlanmadi).

## Ekler
- ADR 0021 (`docs/adr/0021-guvenlik-sertlestirme.md`)
- `docs/runbook/incident-response.md` bolum 6
