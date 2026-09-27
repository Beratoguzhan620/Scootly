# ADR 0020: Faz 4 Desen Envanteri (61-80. gün)

## Durum
Kabul edildi — Faz 4 kapanış dokümanı.

## Kullanılan Desenler

| Desen | Bu problemi çözdü | Reddedilen alternatif |
|---|---|---|
| **Outbox** | Veritabanı yazımı ile mesaj yayınlamanın atomik olmaması riski (66. gün) | İki ayrı işlem (DB yaz + doğrudan yayınla) — kısmi başarısızlıkta veri tutarsızlığı riski taşırdı |
| **Saga (koreografi, basitleştirilmiş)** | Ödeme adımının başarısız olması durumunda aracın kilitli kalmaması (69. gün) | Tam koreografi (3 ayrı tüketici) — bu ölçekte gereksiz karmaşıklık, yeni mesaj kaybı riski (ADR 0015) |
| **Circuit Breaker (Polly)** | Sürekli başarısız olan dış servise (ödeme simülatörü) gereksiz istek göndermeyi durdurma (72. gün) | Sadece retry — devre açılmadan sonsuz denemeye devam ederdi, dış servisi daha da yorardı |
| **Repository** | Domain'in veritabanı teknolojisinden bağımsız kalması (1-3. gün) | Doğrudan DbContext kullanımı — test edilebilirliği ve katman disiplinini zayıflatırdı |
| **Idempotent Consumer** | Aynı mesajın (RabbitMQ'nun "en az bir kez teslim" garantisi yüzünden) iki kez işlenmesi riski (68. gün) | Tüketicinin kendisinin "umarım tekrar gelmez" varsayımıyla çalışması — güvenilmez |
| **Cache-Aside (Redis)** | Sık sorgulanan, nadiren değişen veri (varsayılan araç listesi) için gereksiz veritabanı yükü (53. gün) | Write-through cache — bu senaryoda gereksiz karmaşıklık, cache-aside yeterliydi |

## Aşırı Mühendislik Taraması

**Bulunan potansiyel aşırı mühendislik:**
- `IEventPublisher` (62. gün) — şu an yalnızca `RabbitMqEventPublisher` tarafından uygulanıyor, ikinci bir uygulaması yok. Klasik "tek uygulaması olan arayüz" anti-pattern'i. AMA: test edilebilirlik için (sahte/fake bir uygulama ile test yazılabilmesi) ve gelecekte gerçekten Kafka/Redpanda'ya geçiş ihtimali (75. günde denendi) nedeniyle **bilinçli olarak korunuyor** — bu bir istisna, kural değil.
- `TelemetryStreamProducer`/`Consumer` (75. gün) — projeye hiç entegre edilmedi, yalnızca deneysel. Zaten ADR 0016'da "entegre edilmedi" diye işaretlenmişti, temizlik gerekmiyor.

**Sadeleştirilen bir şey yok** — mevcut soyutlamaların hepsi gerçek bir ihtiyaca karşılık geldiği için, bugün kod tabanından bir şey çıkarılmadı.

## Retro — Tahmin vs Gerçekleşen

16 haftalık (Faz 1-4) planın, konuşma boyunca bazı günlerde sıralama karıştı
(51-60. gün arası telemetri/Redis sırası ters döndü, 61. gün RabbitMQ yerine
bellek içi Channel ile başlandı) ama tüm içerik sonunda (61-72. gün arası
"eksik kapatma" turunda) tamamlandı. Gerçek süre, planın öngördüğünden daha
fazla tur/düzeltme gerektirdi (paket sürüm çakışmaları, boş migration'lar,
CORS sorunları gibi gerçek geliştirme sürtünmeleri) — ama bu, dokümanın
"gerçek hatalarla öğrenme" felsefesiyle tutarlı.

## Dürüstlük Notu (dokümandan)
Mikroservisler bu ölçekte kesinlikle gereksizdir. Tek geliştirici, tek
dağıtım birimi ve tek veritabanıyla mikroservise geçmek öğrenme hızını
düşüren bir hatadır. Scootly, Faz 4 sonunda hâlâ bilinçli olarak bir
modüler monolit.
## Güncelleme (27.09.2026) — Sadeleştirme

Teknik inceleme sonrasında aşağıdakiler kaldırıldı:

- `IEventPublisher` / `RabbitMqEventPublisher`: hiçbir yerde kullanılmıyordu ve outbox'ı atlayan ikinci bir yayın yolu
  sunuyordu. Tek yayın yolu artık outbox (domain olayı, outbox, `OutboxProcessor`).
- `TransactionBehavior`, `PaymentAuthorizedIntegrationEvent`, `Reservation`, `DeviceId`, Worker şablon sınıfı.
- `TelemetryStreamProducer`/`Consumer` üretim kodundan deney testinin yanına taşındı (ADR 0016).

Eklenen desenler: yeniden deneme kuyruğu + DLQ (ADR 0013), publisher confirms ve `SKIP LOCKED` ile çoklu yayıncı
güvenli outbox, domain olaylarının outbox'a aktarılması (ADR 0022), uzlaştırma işi (ADR 0015), kaynak tabanlı
yetkilendirme ve istemci başına rate limiting (ADR 0021).
