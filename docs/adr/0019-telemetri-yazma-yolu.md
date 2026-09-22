# ADR 0019 — Telemetri ayrı yazma yolundan: kanal + COPY

- **Durum:** Kabul edildi
- **Gün:** 44, 51–52
- **Bağlam:** Faz 3, Hafta 11

## Karar

Telemetri verisi:

1. Kendi tablosuna yazılıyor (`telemetry_readings`), `Vehicles`'a değil.
2. API isteği veritabanını beklemiyor; sınırlı bir kanala yazıp hemen dönüyor.
3. Arka plan servisi kanalı gruplar halinde boşaltıp PostgreSQL `COPY`
   protokolüyle yazıyor.

## 1. Neden ayrı tablo

Karar 3'ün pratiğe dökülmesi. 200 araç beş saniyede bir konum bildiriyor;
telemetri `Vehicles` satırlarına yazılsaydı:

- Saniyede onlarca `UPDATE`, kiralama işlemlerinin ihtiyaç duyduğu satırları
  kilitlerdi.
- 38. günde eklenen sürüm damgası her telemetri güncellemesinde artardı ve
  **her kiralama çakışma alırdı** — koruma, koruduğu işlemi engeller hale
  gelirdi.

Yüksek frekanslı bir akışla düşük frekanslı ama kritik bir veri aynı satırı
paylaşmamalı.

## 2. Neden kanal

İsteği veritabanı yazımını bekleterek yanıtlamak, API'nin yanıt süresini
doğrudan yazma hızına bağlar. Kanal ikisini ayırıyor.

### Kanal dolduğunda: atmak

Üç seçenek vardı:

| Seçenek | Karar | Gerekçe |
|---|---|---|
| Beklemek (`Wait`) | Reddedildi | Kanalın çözdüğü problemi geri getirir: yanıt süresi yine yazma hızına bağlanır, üstelik kuyruk dolu olduğu için en kötü anda. |
| Reddetmek (503) | Reddedildi | Cihaz yeniden dener, yük daha da artar. |
| **Atmak (`DropWrite`)** | **Seçildi** | Telemetri kayıp toleranslı: bir ölçüm kaybolursa beş saniye sonra yenisi gelir. |

**Atılan kayıt sayılıyor** (`Interlocked` ile, 57. gün) ve hem HTTP yanıtında
hem logda görünüyor. Sessizce atmak kabul edilemez: fark edilmeyen bir veri
kaybı, ancak bir rapor yanlış çıktığında belli olur.

## 3. Neden COPY

`AddRange` + `SaveChanges` on bin kayıt için çok parametreli `INSERT`
ifadeleri üretir; EF bunları gruplar ama her grup hâlâ ayrıştırılıp planlanır
ve her satır önce değişiklik izleyicisine girer.

`COPY` bu adımların hiçbirini yapmaz: satırlar ikili biçimde tek akışta
sunucuya gider. Ölçümü `system-design.md` → "Toplu yazma (44. gün)".

### Ödenen bedel

`TelemetryBulkWriter` tablo ve sütun adlarını **metin olarak** biliyor. Şema
değişirse derleme hatası vermez, çalışma zamanında patlar. ORM'i atlamanın
bedeli, onun verdiği derleme zamanı güvencesini kaybetmek.

Azaltma: tablo adı `TelemetryReadingConfiguration.TabloAdi` sabitinden geliyor
ve sütun adları `Gun44_TopluYazmaTests` ile doğrulanıyor — adlar yanlış olsa o
test kırmızı olur.

### Arayüz koleksiyon alıyor, tek kayıt değil

`ITelemetryWriter.WriteBatchAsync` bilinçli olarak `IReadOnlyCollection`
alıyor. Tek kayıt alan bir metot olsaydı, çağıran onu döngü içinde çağırır ve
tam da kaçınmak istediğimiz şeyi üretirdi. Toplu yazmayı mümkün kılmak
yeterli değil; **tekil yazmayı imkânsız kılmak** gerekiyordu.

## Kaybedilen veri

Yazma başarısız olduğunda grup kaybediliyor ve yeniden denenmiyor. Gerekçe:
aynı hata tekrarlarsa (veritabanı kapalı) sonsuz döngüye girilir ve kuyruk
dolup daha çok kayıt atılır. Kayıp `Error` seviyesinde loglanıyor.

Dayanıklı bir teslim (outbox, ya da 13. haftada gelecek RabbitMQ) bu
ödünleşimi ortadan kaldıracak. Bugünkü hali bilinçli ve kayıtlı bir borç.
