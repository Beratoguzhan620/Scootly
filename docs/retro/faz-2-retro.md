# Faz 2 Retrospektifi — Gün 21–40

## Faz 2 hedefleri ve durum

Plan Faz 2'yi dört maddeyle "bitti" sayıyor:

| Hedef | Durum |
|---|---|
| 50 eşzamanlı kiralama isteğinde tam olarak 1 sürüş oluşuyor | **Karşılandı** — `Gun38_IyimserEszamanlilikTests` |
| Altı aktörün yetki sınırları testlerle doğrulanmış | **Kısmen** — aşağıya bak |
| Index öncesi/sonrası execution plan çıktısı belgelenmiş | **Karşılandı** — `Gun33_34_IndeksVePlanTests`, `docs/architecture/system-design.md` |
| Deadlock kasıtlı üretilip çözülmüş | **Karşılandı** — `Gun39_DeadlockTests` |

**Yetki sınırları neden kısmen:** Ziyaretçi, Sürücü, Filo Yöneticisi ve Araç
Cihazı doğrulandı. Saha Operatörü ve Denetçi rolleri tanımlı ama henüz hiçbir
uçta kullanılmıyor — çünkü `FieldTask` alan modeli yazılmadı.
`OperatorRegionHandler` yazıldı ve test edildi, uygulanacağı kaynak yok.

## Ne iyi gitti

**Önce gör, sonra çöz sırası işe yaradı.** 36. gündeki test yarış durumunu
gösterdiğinde, 38. günde eklenen korumanın gerçekten çalıştığını ölçebildik.
Test önce yazılmasaydı "çözdüm" demenin bir karşılığı olmazdı.

**Güvenlik kararlarının gerekçesi yazıldı.** Sekiz ADR'nin her biri "neyi
yapmadık ve neden" sorusunu da cevaplıyor. Bu, üç ay sonra "burada neden böyle
yapmışız" sorusuna cevap verecek olan şey.

**Fail-closed varsayılanlar.** 23. günde varsayılan politikayı "kimlik
doğrulaması zorunlu" yapmak, sonradan eklenen her ucu otomatik olarak korumalı
hale getirdi. 25. ve 26. günlerde eklenen uçların hiçbirinde "bunu korumayı
unuttum" durumu yaşanmadı.

## Ne kötü gitti

**Ortam kurulumu, kod yazmaktan uzun sürdü.** Apple Silicon + Parallels +
Windows misafir kombinasyonunda Docker'ın misafirden erişilememesi, 21.–30.
günlerin tamamının veritabanı olmadan yazılmasına yol açtı. Kod derlendi,
birim testleri geçti, ama hiçbiri çalışırken görülmedi. Bu on günün doğrulaması
tek seferde, 30. günden sonra yapıldı.

**Ders:** Ortamı önce kur. "Sonra hallederiz" dediğimiz her şey, sonra
halledilene kadar doğrulanmamış kod olarak birikiyor.

**Bir güvenlik açığı üç gün açık kaldı.** `Reserve` ve `Start` uçlarındaki
IDOR açığı 23. günde fark edildi (controller'lara yorum olarak yazıldı) ama
26. güne kadar kapatılmadı, çünkü plan onu 26. güne koyuyordu. Plana sadık
kalmak doğruydu — ama açık bilinip de bekletilirken depoya push edildi.

**Ders:** Bilinen bir açık, planın sırasını beklemez. Bir dahaki sefere fark
edildiği gün kapatılacak, plan günü de "zaten kapalıydı, tarama onu doğruladı"
diye yazılacak.

**Türkçe yerel ayarı iki kez ısırdı.** Bir kez kabuk betiğinde
(`grep -i identity` eşleşmedi), bir kez de neredeyse yetkilendirme kodunda.
İkincisi yakalandı ve `OrdinalIgnoreCase` kuralı yazıya geçti.

## Tahmin ile gerçekleşen

| | Tahmin | Gerçekleşen |
|---|---|---|
| Faz 2 süresi | 4 hafta (plan) | Sıkıştırılmış, gün başına ayrılan süre plandan az |
| En uzun süren gün | 38 (eşzamanlılık) sanılıyordu | Ortam kurulumu (Docker/ağ) |
| En çok yeniden yazılan | — | Kabuk betikleri (yapıştırma ve yerel ayar sorunları) |

Gün numaraları takvim günü değil, iş paketi olarak ilerledi: bazı günler aynı
oturumda birkaç tanesi birden yapıldı, bazıları ortam sorunlarıyla bölündü.

## Faz 3'e taşınan riskler

1. **Konum verisi saklama politikası uygulanmadı.** ADR 0009 doksan gün diyor,
   silme işini yapacak arka plan servisi yok. Veri birikmeye devam ediyor.
2. **Hız sınırlama hiçbir uçta yok.** Hesap kilidi tek hesabı korur, dağıtılmış
   denemeyi durdurmaz.
3. **Entegrasyon testleri (Testcontainers) bu makinede çalışmıyor.** Ölçüm
   laboratuvarı bu boşluğu kısmen dolduruyor ama hermetik değil.
4. **`Wallet` ve `Tariff` hâlâ yok.** Faz 3'teki ücretlendirme işi bunlara
   bağlı ve 39. gündeki deadlock deneyi bu yüzden `Rides` tablosuyla yapıldı.
