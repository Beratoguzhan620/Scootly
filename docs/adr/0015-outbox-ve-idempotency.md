# ADR 0015: Outbox ve Idempotency — "En Az Bir Kez Teslim + Tekrar Kontrolü"

## Durum
Kabul edildi (Hafta 14, 66–68. günler). ADR 0010'daki ön kararın yerini alıyor.
ADR 0014'teki üç bilinen açığı (ikili yazma, yayıncı onayı, tekrar koruması) kapatıyor.

## Bağlam
Hafta 13'te `CompleteRideCommandHandler` sürüşü kaydedip ardından olayı
doğrudan RabbitMQ'ya gönderiyordu. İki ayrı sisteme iki ayrı yazma, birlikte
garanti edilemiyor (**ikili yazma problemi**): kayıt başarılı olup yayınlama
başarısız olursa sürüşün ücreti hiç hesaplanmıyordu; hata loglanıyor ama olay
kayboluyordu.

Yayınlamayı kayıttan ÖNCE yapmak da çözüm değil: o zaman kayıt başarısız
olduğunda var olmayan bir sürüş için ücret hesaplanırdı.

## Karar

### 1. Outbox (66. gün)
Olay kuyruğa değil, olaya sebep olan değişiklikle **aynı transaction'da**
`OutboxMessages` tablosuna yazılıyor (`IOutboxWriter` → iş birimine ekler,
kaydetmez; kaydı işleyicinin kendi `SaveChangesAsync`'i yapar). Ya ikisi birden
kaydedilir ya hiçbiri.

Yan etkisi önemli: **API artık RabbitMQ'ya hiç bağlanmıyor.** RabbitMQ kapalıyken
de sürüş bitirilebiliyor; olay kaybolmuyor, yalnızca Worker onu kuyruğa
taşıyana kadar gecikiyor.

### 2. Outbox göndericisi (67. gün)
Worker'daki `OutboxDispatcherService` iki saniyede bir bekleyen kayıtları okuyup
kuyruğa taşıyor.

- **"Gönderildi" işareti ancak RabbitMQ onayladıktan sonra.** Yayınlayıcı
  yayıncı onayı (publisher confirm) açık ve `mandatory: true` çalışıyor:
  mesaj reddedilirse ya da hiçbir kuyruğa yönlenemezse istisna, yani kayıt
  "gönderilmedi" olarak kalıyor.
- **`FOR UPDATE SKIP LOCKED`.** İki Worker kopyası aynı satırları okuyup iki kez
  göndermiyor; biri kilitlediğinde diğeri o satırları görmüyor, beklemiyor da.
- **Temizlik.** Gönderilmiş kayıtlar 7 gün, "işlendi" kayıtları 14 gün sonra
  siliniyor. Planın "yaygın tuzak" listesindeki ilk madde: outbox'ı
  temizlememek. Her sürüş en az üç outbox kaydı üretiyor.

### 3. Idempotency (68. gün)
Outbox'ın bedeli: bir olay gönderilip "gönderildi" işareti kaydedilemeden süreç
çökerse aynı olay bir sonraki turda **tekrar** gider. Tüketici tarafında da aynı
şey: işi yapıp onay (ack) gönderemeden çökerse mesaj yeniden gelir.

**"Tam olarak bir kez teslim" iki ayrı sistem arasında kurulamaz.** Kurulabilen
şey: en az bir kez teslim + tekrar kontrolü = **etkisi bir kez**.

- `ProcessedMessages` tablosu, birincil anahtar **(MessageId, Consumer)**.
  Yalnızca MessageId olsaydı, aynı olayı dinleyen ikinci tüketici "zaten
  işlendi" deyip atlardı.
- "İşlendi" kaydı işin kendisiyle **aynı transaction'da** yazılıyor.
  Önce kayıt sonra iş: arada çökmek işi hiç yapılmamış ama "yapıldı" diye
  bırakır. Önce iş sonra kayıt: arada çökmek işi iki kez yaptırır.
- İki tüketici aynı mesajı aynı anda işlerse ikisi de "işlenmedi" görebilir;
  ikincinin kaydı birincil anahtar ihlaliyle reddedilir, onunla birlikte işin
  kendisi de geri alınır, mesaj geçici hata olarak yeniden denenir ve bu sefer
  "işlendi" yolundan çıkar.

### ADR 0010'dan neden ayrıldık
ADR 0010 "her işleyici kendi kontrolünü açıkça yazsın" diye ön karar vermişti.
İtirazı öznitelik + yansıma "sihrine"ydi, ve o itiraz hâlâ geçerli: burada
öznitelik yok. Ama kontrol her işleyiciye de yazılmıyor; `IdempotencyBehavior`
**tek bir yerden, açıkça** çağrılıyor (Worker'ın tüketici servisi ve webhook
ucu). Her işleyiciye yazmak, yeni bir tüketici eklendiğinde unutulabilecek bir
satır demekti — ve unutulduğunu hiçbir test göstermezdi, çünkü tekrar teslim
yalnızca çökme anında olur.

## Sonuçlar

**Olumlu**
- Sürüş bitirme ve olay yayınlama birlikte ya başarılı ya başarısız.
- API'nin RabbitMQ'ya bağımlılığı kalktı.
- Aynı sürüş için iki ücret, iki ödeme, iki borç oluşamıyor.

**Olumsuz / bilinen açıklar**
- Olaylar artık en az 0–2 saniye gecikmeli (göndericinin aralığı).
- Sıra garantisi YOK: outbox kayıtları oluşturulma sırasıyla okunuyor ama
  tüketici tarafında paralel işleme ve yeniden denemeler sırayı bozabilir. Bu
  saga'da sorun değil, çünkü her adım bir öncekinin sonucuna dayanıyor.
- "İşlendi" kaydı 14 gün tutuluyor; 14 günden eski bir mesajın tekrar gelmesi
  artık tanınmaz. Bekleme kuyruğu 5 saniye, outbox en fazla birkaç tur geride;
  14 gün geniş bir pay, ama sınırsız değil.
- Gönderimi sürekli başarısız olan bir outbox kaydı (`Attempts` artıyor)
  için alarm yok; `LastError` sütunundan elle bakılıyor.
