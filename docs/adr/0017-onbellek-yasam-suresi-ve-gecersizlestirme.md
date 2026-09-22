# ADR 0017 — Önbellek yaşam süreleri ve geçersizleştirme tablosu

- **Durum:** Kabul edildi
- **Gün:** 46–49
- **Bağlam:** Faz 3, Hafta 10 — önbellekleme

## Karar

Her önbellek girdisinin **zorunlu** bir yaşam süresi (TTL) var. Ayrıca hangi
olayda hangi önbelleğin silineceği aşağıdaki tabloda yazılı.

## Yaşam süreleri

| Önbellek | TTL | Gerekçe |
|---|---|---|
| Aktif tarife | 5 dakika | Tarife nadiren ve planlı değişir. En kötü durumda 5 dakika eski fiyat görünür. **Fatura kesilirken tarife önbellekten DEĞİL veritabanından okunur.** |
| Yakındaki araçlar | 5 saniye | Araç durumu saniyeler içinde değişiyor. Harita ekranının yenilenme aralığından kısa: kullanıcı aynı ekranda iki kez bakarsa ikincisi taze veri görüyor. |
| Tarife HTTP yanıtı (output cache) | 1 dakika | Veri önbelleğinin üstünde ikinci katman. Serileştirme dahil tüm yanıtı saklıyor; isabet halinde controller'a hiç girilmiyor. |

### TTL neden zorunlu

`ICacheService.SetAsync` yaşam süresini isteğe bağlı almıyor. Süresiz
yazılabilen bir önbellek, günün birinde silinmesi unutulmuş bir girdi yüzünden
kullanıcıya **süresiz** bayat veri gösterir.

Zorunlu TTL, geçersizleştirme tamamen başarısız olsa bile bayatlığın bir üst
sınırı olmasını garanti ediyor. Redis'e ulaşılamadığı için `RemoveAsync`
başarısız olduğunda, tek güvence bu sınır — bu yüzden o hata `Error`
seviyesinde loglanıyor.

## Geçersizleştirme tablosu

| Olay | Silinen | Nerede |
|---|---|---|
| Araç rezerve edildi | Harita önbelleğinin tamamı (`yakin:*`) | `ReserveVehicleCommandHandler` |
| Sürüş başladı | Harita önbelleğinin tamamı | `StartRideCommandHandler` |
| Sürüş tamamlandı | Harita önbelleğinin tamamı | `CompleteRideCommandHandler` |
| Rezervasyon zaman aşımıyla düştü | Harita önbelleğinin tamamı | `ReservationTimeoutService` |
| Sürüş terk edilmiş olarak kapandı | Harita önbelleğinin tamamı | `AbandonedRideDetector` |
| Tarife değişti | *(henüz bir uç yok)* | — |

### Neden tek tek değil, önek silerek

Bir aracın durumu değiştiğinde onu hangi önbellek girdilerinin kapsadığını
hesaplamak gerekirdi: araç birden fazla yarıçapın, birden fazla sayfanın
içinde olabilir. O hesabı yapmak yerine `yakin:*` önekinin tamamı siliniyor.

**Bedeli dürüstçe:** bir aracın değişmesi bütün harita önbelleğini düşürüyor.
Beş saniyelik TTL'de bunun maliyeti düşük — ama trafik arttığında (her saniye
onlarca kiralama) önbellek sürekli boş kalır ve hiçbir işe yaramaz hale gelir.
O noktada bölgesel anahtarlama (yalnızca ilgili ızgara hücresini silmek)
gerekecek. Teknik borç listesinde.

Silme işi `KEYS` ile değil `SCAN` ile yapılıyor: `KEYS` tüm anahtar uzayını
tek seferde tarar ve Redis tek iş parçacıklı olduğu için o süre boyunca bütün
istekleri bekletir.

### Sıra: kaydet, sonra sil

Geçersizleştirme her zaman `SaveChangesAsync`'ten **sonra**. Önce silinseydi,
kaydetme ile silme arasındaki aralıkta gelen bir okuma eski veriyi yeniden
önbelleğe yazar ve silme boşa giderdi.

## Asla önbelleklenmeyecekler

- **Kullanıcıya özel veri, ortak bir anahtarla.** Başkasının verisini başkasına
  gösterir. Çıktı önbelleği (output cache) yalnızca kimlik doğrulaması olmayan
  uçlarda kullanılıyor, tam olarak bu yüzden.
- **Kimlik ve yetki kararları.** İptal edilmiş bir yetkiyi yaşatır.
- **Para hesabına giren tutarlar.** Bayat fiyattan fatura keser.

## Cache stampede

Aynı anahtar için aynı anda gelen N istek, ıska durumunda N kez veritabanına
gidiyor. Kilitle çözülmedi: her ıskada bir dağıtık kilit almanın maliyeti,
önlediği fazladan sorgudan yüksek. Beş saniyelik TTL'de aynı anahtara aynı
anda gelen istek sayısı zaten sınırlı. Trafik arttığında yeniden ölçülecek.
