# ADR 0044: Faz 5 ve proje final retrosu

Durum: kabul edildi (110. gun). Bu retro yalnizca repo kayitlarina ve olculen sonuclara dayanir; kisisel degerlendirme icermez.

## Kanita dayali: ne calisti

- Outbox deseni: is verisi ve olay tek `SaveChanges` icinde yazilir (`ScootlyDbContext`); RabbitMQ kesintisinde olaylar kaybolmadi, birikti ve donunce yayinlandi (106. gun olculdu).
- Odeme guvenligi uc katmanli: saglayici idempotency anahtari, `PaymentStatus == Pending` kontrolu, webhook `EventId` ile tekrar yutma. Ek olarak Worker uzlastirmasi (ADR 0023).
- Kasitli ariza provalari (ADR 0042) gercek bulgu uretti: 106. gunde tuketicilerin kesinti sonrasi toparlanmamasi; 109. gunde kok neden bulunup duzeltildi.
- Postmortem sureci (27.09.2026 sizmis sirlar) somut aksiyonlar uretti: rotasyon, `ValidateOnStart`, `.env`, CI paket taramasi.
- Test piramidi: domain birim testleri (kapsam 109. gun olculdu: satir %90,9, dal %91,7, yalnizca `Scootly.Domain`), Infrastructure entegrasyon testleri (Testcontainers), Playwright E2E (ADR 0040), k6 yuk testi (ADR 0039).

## Kanita dayali: ne zor oldu / ne yanlis cikti

- 106. gunde RabbitMQ hatasinin kok nedeni once hipotezdi. 109. gunde kontrol deneyi ("saglayiciya dokunulmazsa tuketici toparlanir mi") ilk kurulumda gecersizdi, cunku test yardimcisi saglayiciya kendisi dokunuyordu; bagimsiz bir baglanti fabrikasiyla yeniden kurulunca hipotez dogrulandi. Ders: kontrol deneyini once denetle.
- 108b'de nesne deposu olarak MinIO yerine SeaweedFS secildi (ADR 0043); prod'a baglanmadi (acik borc).
- Onceki gunlerin bircok notu "olculmedi / denenmedi" olarak kaldi; bu bir borc, ama toplu "bilincli birakildi" olarak siniflandirilmadi.

## Acik kalanlar

`technical-debt.md` "Gun 109: son durum" ve `system-design.md` bolum 3 (risk listesi).
