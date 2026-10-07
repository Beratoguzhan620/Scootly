# ADR 0040: Playwright ile Uctan Uca (E2E) Testler (104. Gun)

## Durum
Kabul edildi.

## Karar
- Tarayici testleri `tests/Scootly.E2E.Tests` projesinde: xunit v3 + `Microsoft.Playwright` 1.63.0 (nuget.org'da gorulen en yuksek surum; `Microsoft.Playwright.Xunit.v3` paketi kullanilmadi, kendi xunit surum bagimliligi repodaki 3.2.2 pini ile cakisabilir diye).
- Yigin: `ScootlyApiFactory` (Gun 28-ten beri var) gecici Postgres'i kaldirir, migration'lari uygular ve veriyi tohumlar (kullanici + rol, arac, `FieldTask`). Fabrikanin Api sunucusu surec ici kalir; tarayici onu kullanmaz. `Scootly.Mvc` ise ayri bir surec olarak `bin/{Debug|Release}` altindan, bos bir portta, `Development` ortaminda (http) baslatilir. Redis ve mesajlasma kapali.
- Her test kendi benzersiz verisini uretir (rastgele marka soneki, kendi kullanicisi, kendi gorevi); testler birbirine bagli degil.
- Secicilerde etiket/rol/metin kullanilir (`GetByLabel`, `GetByRole`); sayfalarda `data-testid` yok, eklenmedi.
- Test govdesi `fixture.RunAsync(...)` icinde calisir: hata olursa ekran goruntusu, sayfa HTML'i ve Mvc surec ciktisi `E2E_ARTIFACTS_DIR` altina yazilir, hata aynen tekrar firlatilir.
- Etiket: `[Trait("Category", "E2E")]`. CI'daki hizli filtre `Category!=Measurement&Category!=Experiment&Category!=E2E` oldu (README'deki ayni komut dahil).
- CI'da `e2e` ayri bir istir; `build-and-test`'i beklemez ve onu bloklamaz. Tarayici `pwsh .../playwright.ps1 install --with-deps chromium` ile kurulur; hata olursa artifact yuklenir.
- Kapsam: (1) FleetManager: giris -> arac listesi -> duzenle -> kaydet -> listede yeni marka ve menzil; (2) FieldOperator: giris -> gorev ustlen -> satirda `Assigned` + veritabaninda `Assigned` ve `AssignedTo` = operator.

## Olculenler
- Yerel: duman testi + 2 senaryo, 3/3 yesil (yaklasik 13 sn, Docker Desktop'ta Testcontainers dahil).
- Testin kirilabildigi: beklenen menzil gecici olarak "41" yapilinca test kirmizi oldu; Playwright mesaji hucrenin "40" oldugunu gosterdi. Ayni kosuda ekran goruntusu, HTML ve Mvc logu uretildi.
- CI (ubuntu-24.04): 3/3 yesil, test suresi 4 sn; `pwsh` runner'da var, `install --with-deps chromium` calisti (apt ile 9 yeni paket, Chromium ve headless shell her calismada yeniden indirildi).

## Olculmeyenler
- `e2e` isinin toplam suresi ve tarayici onbellegiyle kazanilacak sure.
- Kararsizlik (flakiness): testler yalnizca birkac kez kosuldu.
- `Production` davranisi: Secure cookie, HSTS, Nginx TLS yolu (E2E `Development` + http ile kosar).
- Harita sayfasi, arac olusturma, gorev tamamlama, dogrulama hatasi ve yetkisiz erisim (AccessDenied) yollari.
- E2E'nin kod kapsamina etkisi: Mvc ayri surecte kostugu icin coverlet bu kodu buyuk olasilikla olcmez (tasarimdan; denenmedi).

## Kisitlar
- Mvc yolu test derleme yapilandirmasina baglidir (`#if DEBUG`); test `-c Release` ile derlenmisse Mvc de Release derlenmis olmalidir (ProjectReference ikisini ayni yapilandirmada derler).
- Bos port, Mvc baslamadan once dinleyici kapatilarak bulunur; teorik olarak baska bir surec araya girebilir.
- Gelistirici makinesinde Mvc `Development` ortaminda acildigi icin ayni makinedeki user-secrets okunabilir; ortam degiskenleri ayni anahtarlari ezer ama baska anahtarlar sizabilir (CI'da user-secrets yok).
