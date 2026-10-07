# Scootly'yi anlatmak: konuşma metni

Bu metin Scootly'yi hiç görmemiş birine baştan sona anlatmak için yazıldı. Her bölüm bir "anlatılacaklar" kısmı ve
gerekirse bir "söylenebilecek cümle" içerir. 15 dakikalık kısa sürüm için `15-dk-sunum-plani.md`'ye bakın; bu metin
uzun sürümdür (yaklaşık 30–40 dk) ve sorulara hazırlık için de kullanılabilir. Sayılar 7 Ekim 2026'da kayıtlıdır.

---

## 1. Tek cümleyle Scootly

> "Scootly, şehir içi elektrikli scooter paylaşımı için yazılmış bir backend ve filo yönetim paneli. Amacımız çok
> özellik değil; bir sürüşün, ödemenin ve araç durumunun arıza anında bile doğru kalmasıydı."

Bir öğrenme/portföy projesi: gerçek scooter, gerçek ödeme sağlayıcısı yok. Onların yerine iki simülatör var.

---

## 2. Kim kullanıyor? (roller)

| Kullanıcı | Ne yapar? |
|---|---|
| **Sürücü (Driver)** | Haritada müsait scooter'ı görür, 10 dakikalığına rezerve eder, sürüşe başlar, bitirir, ücretini öder, geçmişini görür. |
| **Filo yöneticisi (FleetManager)** | Araç ekler ve düzenler, rol verir, her şeyi görür. |
| **Saha operatörü (FieldOperator)** | Bataryası düşen veya kontrol gereken araçların görevlerini üstlenir, tamamlar (isteğe bağlı fotoğrafla), aracı bakıma alır ya da hizmete döndürür. |
| **Cihaz (Device)** | Scooter'ların konum ve batarya bilgisini toplu olarak gönderen ağ geçidi. |

> "Aynı sistem dört farklı kullanıcıya hizmet ediyor ve her biri yalnızca kendi işini yapabiliyor; bu ayrım hem
> rollerle hem de 'bu sürüş senin mi?' gibi kaynak sahipliği kontrolleriyle sağlanıyor."

---

## 3. Uygulamanın parçaları

Sistem tek bir kod tabanında, ama ayrı çalışan beş uygulamadan oluşuyor:

1. **Api** — mobil uygulamanın konuşacağı REST API. Giriş, araç listesi, rezervasyon, sürüş, ödeme durumu, hesap
   işlemleri. Canlı bildirimler için SignalR hub'ı da burada.
2. **Mvc (web paneli)** — filo ekibinin tarayıcıdan kullandığı yönetim paneli: araçlar, canlı harita, saha görevleri.
3. **Worker** — kimsenin tıklamadığı ama sürekli çalışan işler: süresi dolan rezervasyonu düşürmek, unutulan sürüşü
   kapatmak, ödenmemiş ücreti yeniden denemek, eski veriyi silmek, düşük bataryalı araçlara görev açmak.
4. **PaymentSimulator** — gerçek bir ödeme sağlayıcısı gibi davranır: bazen onaylar, bazen reddeder, bazen "şu an
   hizmet veremiyorum" der ve sonucu ayrıca imzalı bir bildirimle (webhook) haber verir.
5. **DeviceSimulator** — scooter'ları hareket ettirip bataryalarını düşüren, telemetri gönderen sahte cihaz ağ geçidi.

Bunların arkasında üç altyapı servisi var: **PostgreSQL** (asıl veri), **RabbitMQ** (uygulamalar arası mesajlar),
**Redis** (önbellek ve panel kopyalarının ortak anahtarları).

### Kodun katmanları (Clean Architecture)

```
Api / Mvc / Worker  →  Infrastructure  →  Application  →  Domain
```

- **Domain**: iş kuralları. "Müsait olmayan araç rezerve edilemez", "%10'un altında batarya kiralanamaz", "ücret =
  başlamış her dakika × 2,5 (en az 1 dakika)". Hiçbir kütüphaneye bağımlı değil; saf C#.
- **Application**: kullanım senaryoları (rezerve et, sürüşü bitir, ödemeyi tahsil et). Veritabanını bilmez, yalnızca
  soyutlamalarla konuşur.
- **Infrastructure**: gerçek teknoloji: EF Core + PostgreSQL, RabbitMQ, Redis, kimlik, ödeme istemcisi, dosya deposu.
- **Api / Mvc / Worker**: dış dünyayla konuşan ince katmanlar.

> "Bu ayrımın faydası şu: iş kuralını test etmek için veritabanı açmamız gerekmiyor. Ve bu kural sadece bir niyet
> değil; mimari testleri, Domain'in başka bir katmana bağımlı olduğu an derlemeyi kırmızıya düşürüyor."

---

## 4. Bir sürüşün yolculuğu (ana bölüm)

Bu bölümü yavaş anlatın; sistemin neredeyse her parçası bu akışta görünür.

1. **Aracı bulmak.** Sürücü `GET /api/v1/vehicles` ile yakındaki araçları ister. Yalnızca **müsait** araçlar döner.
   Sürüşteki araçların konumu gizlidir; aksi halde biri listeyi yoklayarak bir sürücünün güzergâhını izleyebilirdi.
   Varsayılan liste 5 saniye Redis'te önbelleklenir.
2. **Rezerve etmek.** `POST /vehicles/{id}/reserve`. Araç 10 dakikalığına o sürücüye ayrılır. Aynı anda 50 kişi aynı
   aracı isterse yalnızca biri kazanır: veritabanı satırındaki sürüm (`xmin`) değişmişse diğerleri "araç bu sırada
   başkası tarafından rezerve edildi" cevabını alır. Bir sürücünün ikinci rezervasyonunu ise veritabanındaki benzersiz
   indeks bile reddeder.
3. **Sürüşe başlamak.** `POST /api/rides/start`. Yalnızca rezervasyonu yapan sürücü başlatabilir. Sürüş kaydı açılır,
   araç `InRide` olur.
4. **Sürüşü bitirmek.** `POST /api/rides/{id}/complete`. Cevap **202 Accepted**: ücret hesaplanır ama ödeme bu istekte
   alınmaz. Aracın son konumunu cihaz bildiriyorsa (son 2 dakika) o esas alınır; sürücünün telefonu değil.
5. **Tek transaction.** Sürüş "tamamlandı", araç "müsait" ve "bu sürüş tamamlandı" olayı aynı anda, tek bir veritabanı
   işleminde yazılır. Olay bir **outbox** tablosuna gider. Ya üçü birlikte olur ya hiçbiri.
6. **Outbox yayını.** Arka planda bir yayıncı, outbox'taki olayları RabbitMQ'ya gönderir ve broker "aldım" diyene kadar
   "gönderildi" saymaz. RabbitMQ o an kapalıysa olay kaybolmaz, outbox'ta bekler.
7. **Tahsilat.** Ödeme tüketicisi olayı alır ve ödeme sağlayıcısına gider. Her denemenin bir **idempotency anahtarı**
   vardır; aynı istek iki kez giderse sağlayıcı ikinci kez para çekmez.
8. **Webhook.** Sağlayıcı sonucu ayrıca imzalı bir bildirimle haber verir. İmza ve zaman damgası doğrulanır; aynı
   bildirim iki kez gelirse ikincisi yok sayılır.
9. **Uzlaştırma.** Worker dakikada bir "ödeme bekleyen" sürüşlere bakar: reddedilenleri belli aralıkla tekrar dener,
   mesajı bir şekilde kaybolmuş olanları da yakalar. 5 retten sonra sürüş `Failed` olur ve operasyona devredilir.
10. **Sonuç.** Sürücü `GET /rides/{id}/payment-status` ile `Pending → Paid` geçişini görür.

> "Neden bu kadar parça? Çünkü her biri tek başına yetmiyor. Outbox olay kaybını, idempotency anahtarı çift
> tahsilatı, uzlaştırma da kaybolan mesajı önlüyor. Üçü birlikte çalışınca ödeme arızada da doğru kalıyor."

---

## 5. Canlı harita ve telemetri

- Cihaz ağ geçidi `POST /api/telemetry/batch` ile tek seferde 500'e kadar okuma gönderir. Okumalar bellekte sınırlı bir
  kuyruğa alınır, arka planda partiler halinde veritabanına yazılır. Kuyruk doluysa sistem okuma düşürmez; cihaza
  "5 saniye sonra tekrar dene" (503 + `Retry-After`) der.
- Batarya %20'nin altına ilk kez indiğinde "batarya düşük" olayı oluşur ve Worker saha ekibine görev açar. Olay bir
  şekilde kaçarsa (araç zaten düşük bataryayla kaydedildiyse) 15 dakikada bir çalışan tarama eksik görevi açar.
- Araç durumu değiştiğinde (rezerve edildi, sürüşe çıktı, bakıma alındı) olay her Api kopyasına ulaşır ve SignalR
  ile, aracın bulunduğu **hizmet bölgesinin** grubuna yayınlanır. Bölge, aracın konumunun hangi poligonun içinde
  kaldığına bakılarak bulunur.

> "Haritadaki renk değişimi bir sayfa yenilemesi değil; veritabanından RabbitMQ'ya, oradan SignalR'a uzanan gerçek
> zamanlı bir akış."

---

## 6. Saha operasyonu

- **Görevler** sayfasında açık görevler listelenir; operatör birini **üstlenir**, işi yapınca **tamamlar**. İsterse
  fotoğraf ekler (JPEG/PNG, en fazla 5 MB). Dosya türüne uzantıdan değil, içeriğin ilk baytlarından karar verilir;
  `.jpg` adı verilmiş bir program reddedilir. Fotoğraf S3 uyumlu bir depoda tutulur ve 60 saniyelik imzalı bağlantıyla açılır.
- **Terk edilmiş sürüş:** 2 saatten uzun süren sürüşü sistem kapatır, ücretlendirir, aracı bakıma alır ve bir denetim
  görevi açar. Operatör kontrol edince aracı **Araçlar** sayfasından "Hizmete döndür" ile yeniden kiralanabilir yapar.
- **Kayıp araç:** sinyal vermeyen ya da çalınan araç "Kayıp" işaretlenir, bulununca hizmete döner.

---

## 7. Güvenlik ve gizlilik

Anlatırken her maddeyi "ne riskti → ne yaptık" diye kurun.

- **Sırlar:** parolalar ve anahtarlar kodda değil; geliştirmede user-secrets, Docker'da git'e girmeyen `.env`. Eksik ya
  da kısa bir anahtar varsa uygulama açılmaz. Geçmişte depoya giren eski anahtarlar 27 Eylül'de yenilendi.
- **Kimlik:** API'de JWT, panelde cookie. 5 hatalı girişte hesap 15 dakika kilitlenir. Giriş ve kayıt uçlarında IP
  başına dakikada 10 istek sınırı var.
- **Yetki:** rollerin yanında kaynak sahipliği: başkasının sürüşünü göremezsiniz, bitiremezsiniz; denerseniz "yok" cevabı
  alırsınız (varlığı bile sızdırılmaz).
- **Token iptali:** JWT normalde süresi dolana kadar geçerlidir. Biz her token'a kullanıcının güvenlik damgasını koyduk;
  rol değişince, parola değişince veya hesap silinince eski token'lar anında geçersiz oluyor.
- **Panelin harita token'ı:** panel, haritanın canlı bağlantısı için ayrı bir anahtarla, rol taşımayan ve yalnızca hub'da
  geçerli bir token üretir. Panel ana API anahtarını bilmez; ele geçirilse bile API'yi yönetemez.
- **Veritabanı:** uygulamalar yalnızca veri okuyup yazabilen bir rolle bağlanır; tablo silemez, şema değiştiremez.
- **KVKK:** telemetri 30 gün, sürüş konumları 90 gün tutulur; hesap silinince konumlar hemen anonimleşir. Sürüşteki
  araçların konumu herkese açık değildir.

---

## 8. Nasıl test ettik?

| Katman | Ne test ediliyor? | Sayı |
|---|---|---|
| Domain | İş kuralları, durum makineleri, tarife | 112 |
| Application | Kullanım senaryoları, çakışma ve yeniden deneme davranışı | 68 |
| Infrastructure | Gerçek PostgreSQL ve RabbitMQ ile: outbox, retry → DLQ, broker kesintisinden kurtarma | 61 |
| Api | Uçtan uca HTTP: yetki, IDOR, token iptali, hesap silme, rate limit, webhook sahteciliği | 76 |
| Worker / Mvc | Arka plan işleri; panelin girişi, yetkisi, işlemleri, hata sayfaları | 7 / 14 |
| Eşzamanlılık / Mimari | Yarış durumları, deadlock, N+1 / katman kuralları | 10 / 9 |
| E2E | Gerçek tarayıcı (Playwright) ile panel akışları | 8 |

Toplam 359 test geçiyor, 6 test canlı fotoğraf deposu olmadan atlanıyor. Testler gerçek PostgreSQL ve RabbitMQ'yu
Docker'da geçici olarak açıp kapatıyor (Testcontainers); sahte veritabanı kullanılmıyor.

> "Bir hatayı 'düzelttik' demeden önce onu kırmızı gösteren testi yazmaya çalıştık. Örneğin RabbitMQ kesintisi
> testinde düzeltmeden önce test kırmızıydı, sonra yeşil oldu."

---

## 9. Çalıştırma ve işletme

- Her uygulamanın Docker imajı var; tek komutla (`docker compose --profile app up -d --build`) tüm sistem açılır.
  Açılış sırası otomatik: önce veritabanı şeması uygulanır, sonra uygulama rolü hazırlanır, sonra uygulamalar başlar.
- Üretim benzeri yığında önde Nginx (TLS) var, Api ve panel ikişer kopya çalışır; mavi-yeşil dağıtım iskeleti hazır.
- Gözlemlenebilirlik: yapılandırılmış loglar (Seq), dağıtık izleme (Jaeger) ve metrikler (Prometheus + Grafana). Bir
  isteğin Api'den RabbitMQ'ya, oradan ödemeye kadar tek bir iz olarak takibi mümkün.
- CI: her push'ta sır taraması, biçim kontrolü, derleme, testler, imaj üretimi ve imaj güvenlik taraması. Sürüm etiketi
  atılınca imajlar GitHub Container Registry'e yayınlanır.
- Kasıtlı arıza provası yaptık: PostgreSQL, RabbitMQ ve Redis'i bilerek kapattık. RabbitMQ provası gerçek bir hata buldu
  (tüketiciler toparlanmıyordu), kök nedeni bulunup düzeltildi.

---

## 10. Proje sonu incelemesi: ne bulduk?

Bu bölüm dürüstlüğü gösterir; kısa tutun ama atlamayın.

Proje bitince kodu baştan sona kıdemli bir gözle inceledik ve borç listesinde olmayan sorunlar bulduk:

- **Harita poligonları:** hizmet bölgelerinin köşe sırası veritabanından karışık gelebiliyordu; 3 bölge × 50 noktada bile
  ölçtük. Sınırı sırası korunan tek bir JSON dizisine taşıdık ve mevcut veriyi sırasıyla aktaran bir migration yazdık.
- **Gizlilik:** anonim kullanıcılar sürüşteki araçların canlı konumunu görebiliyordu. Kapattık.
- **Panel eksikliği:** terk edilen sürüşten sonra bakıma alınan aracı panelden geri döndürmenin yolu yoktu; filo zamanla
  küçülürdü. Butonları ekledik.
- **Güvenlik:** harita sayfası tam yetkili bir token sızdırıyordu, token'lar iptal edilemiyordu, uygulamalar veritabanına
  süper kullanıcıyla bağlanıyordu, ilk yönetici ayarı var olan bir hesabı yönetici yapabiliyordu. Hepsi düzeltildi.
- **Temizlik:** kullanılmayan kod, 48 kullanılmayan kütüphane dosyası, şablon sayfaları ve bir log seli kaldırıldı.

> "İnceleme, testlerin hepsi yeşilken bile gerçek hatalar olabileceğini gösterdi. Poligon hatası, büyük veriyle bir
> test olmadığı için gözden kaçmıştı; şimdi o test var."

---

## 11. Canlı demo senaryosu (5 dk)

Ön koşul: yığın açık (`docker compose --profile app up -d`), DeviceSimulator çalışıyor, en az birkaç araç kayıtlı.

1. **Panel girişi:** http://127.0.0.1:5096 → yönetici hesabıyla giriş. Panelde aktif sürüşler ve düşük bataryalı araçlar.
2. **Harita:** araçlar renkli noktalar; simülatör çalıştıkça yerleri değişir. "Bağlı" rozeti SignalR bağlantısını gösterir.
3. **API ile sürüş** (`src/Scootly.Api/Scootly.Api.http` dosyasından, sırayla):
   kayıt → giriş → müsait araçlar → rezerve → başlat → bitir (202) → ödeme durumu (`Pending`, birkaç saniye sonra `Paid`).
   Rezerve ettiğiniz anda haritadaki aracın renginin turuncuya döndüğünü gösterin.
4. **Saha:** Görevler sayfasında düşük bataryalı bir aracın görevini üstlenin ve tamamlayın.
5. **Araçlar:** bir aracı bakıma alın, sonra hizmete döndürün.

Demo aksarsa durun ve bölüm 4'teki akışı anlatmaya devam edin; akış zaten anlatılmış olur.

---

## 12. Sık sorulan sorular

**Neden 202 Accepted, neden ödemeyi hemen almıyorsunuz?**
Ödeme sağlayıcısı yavaşlayabilir ya da geçici hata verebilir. Sürücüyü bekletmek yerine sürüşü hemen kapatıp tahsilatı
arka planda yapıyoruz; sonuç `payment-status` ile izleniyor.

**Aynı mesaj iki kez gelirse ne olur?**
Tahsilat yalnızca "bekleyen" sürüş için yapılır, sağlayıcıya aynı idempotency anahtarı gider, webhook olay kimliğiyle
tekrarı yok sayar. Yani en kötü ihtimalle aynı işlem zararsızca tekrarlanır, para iki kez çekilmez.

**RabbitMQ çökerse sürüşler ne olur?**
Sürüşler bitmeye devam eder; olaylar outbox'ta birikir. RabbitMQ dönünce yayınlanır, tüketiciler kendiliğinden yeniden
bağlanır. Bunu kasıtlı arıza provasıyla ölçtük.

**İki kişi aynı aracı aynı anda kiralarsa?**
Biri kazanır. Uygulama iyimser kilitleme yapar; veritabanında da benzersiz indeksler var. 50 eşzamanlı istekle test edildi.

**Neden mikroservis değil?**
Tek ekip, tek veritabanı ve öğrenme amacı için modüler monolit yeterli. Sınırlar yine de net: bağlamlar olaylarla
konuşuyor, katmanlar testle korunuyor (ADR 0017).

**Gerçek ödeme ya da cihaz entegrasyonu var mı?**
Hayır, ikisi de simülatör. Uygulama kodu simülatörü değil bir soyutlamayı bilir; gerçek sağlayıcıya geçiş istemci
sınıfını değiştirmek demektir.

**Neyi yapmadınız?**
Parola sıfırlama ve e-posta doğrulama (e-posta sağlayıcısı gerektirir), park yasağı bölgesi kuralı, ölü mektup kuyruğu
alarmı, üretimde gözlemlenebilirlik. Hepsi `docs/backlog/technical-debt.md`'de gerekçesiyle yazılı.

---

## 13. Kapanış

> "Scootly'de en çok öğrendiğimiz şey, dağıtık bir akışın doğruluğunun tek bir sihirli çözümle değil, birbirini
> tamamlayan küçük kararlarla sağlandığıydı: aynı transaction'da outbox, idempotency anahtarı, uzlaştırma, iyimser
> kilitleme. İkinci ders: arızayı bilerek çıkarmak gerçek hatayı buldu. Üçüncüsü: neyi ölçmediğimizi açıkça yazmak,
> ölçtüklerimize olan güveni artırdı."

Kaynaklar: `README.md`, `docs/architecture/system-design.md`, `docs/adr/` (46 karar kaydı), `docs/runbook/`.
