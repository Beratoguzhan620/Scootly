# ADR 0015: Ödeme Saga'sı — Koreografi Yaklaşımı ve Basitleştirme

## Durum
Kabul edildi.

## Bağlam
Dokümanın 69. günü, ödeme akışını "PaymentAuthorizationRequested olayı yayınla,
ayrı bir tüketici dinleyip ödeme simülatörünü çağırsın, PaymentAuthorizedConsumer
sonucu işlesin" şeklinde üç ayrı tüketiciye bölmeyi öneriyordu (saf koreografi).

## Karar
Bunun yerine, RideCompletedMessageConsumer içinde ödeme çağrısı DOĞRUDAN yapıldı
— ayrı bir PaymentAuthorizationRequested/PaymentAuthorizedConsumer çifti
oluşturulmadı. Nedeni: zaten idempotent ve hataya dayanıklı (retry+DLQ) bir
tüketici içindeyiz; bu adımı ikiye bölmek, aradaki bir mesaj kaybında ("ücret
hesaplandı ama ödeme hiç istenmedi" gibi) yeni bir tutarsızlık penceresi
açardı. Tek bir tüketici içinde senkron çağrı, bu riski ortadan kaldırıyor.

Araç serbestliği, ödeme sonucundan bağımsız olarak zaten CompleteRideCommandHandler
içinde (66. gün) gerçekleşiyor — bu, dokümanın endişe ettiği "ödeme başarısız
olursa araç sonsuza kadar kilitli kalır" riskini mimari olarak baştan ortadan
kaldırıyor. Telafi mantığı, yalnızca sürüşün Fare/Status alanlarını günceller.

## Alternatif: Tam koreografi (3 ayrı tüketici, 2 ayrı entegrasyon olayı)
Dokümanın orijinal önerisi.

## Neden Seçilmedi
Bu projenin ölçeğinde (tek bir saga, iki adım), üç ayrı tüketiciye bölmek
gereksiz karmaşıklık ve yeni bir mesaj kaybı riski ekliyor. Gerçekten çok
adımlı, bağımsız servislerin sorumlu olduğu bir saga'da (örnek: 5+ mikroservis)
koreografi mantıklı olurdu — burada değil.