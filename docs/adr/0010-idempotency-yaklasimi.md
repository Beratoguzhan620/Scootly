# ADR 0010: Idempotency İşaretleme Yaklaşımı (Ön Karar)

## Durum
Ön araştırma tamamlandı, gerçek uygulama 13-14. haftada (mesajlaşma/outbox geldiğinde)
yapılacak.

## Bağlam
İleride (RabbitMQ ile mesaj tüketimi başladığında), aynı mesajın birden fazla kez
işlenmesini önlemek için bir "bu işlem daha önce yapıldı mı" kontrolü gerekecek.
İki yaklaşım değerlendirildi: (a) bir `[Idempotent]` attribute'ü ile işaretleyip
bir middleware/interceptor'da otomatik kontrol etmek, (b) `IIdempotentOperation`
gibi bir arayüz ile her handler'ın kendi kontrolünü elle yazması.

## Karşılaştırma

**Attribute yaklaşımı:**
- Artı: Handler kodu temiz kalır, işaretleme deklaratif.
- Eksi: Attribute'lar reflection gerektirir (küçük bir performans maliyeti),
  hangi alanın "mesaj kimliği" olduğunu attribute'a nasıl bildireceğimiz
  belirsiz (constructor parametresi mi, property mi), test etmesi daha zor
  (attribute'un etkisini test etmek için gerçek pipeline'ı çalıştırman gerekir).

**Arayüz yaklaşımı:**
- Artı: Açık ve okunabilir — hangi handler'ın idempotent olduğu kod okunarak
  anlaşılır. Test etmesi kolay (arayüzü uygulayan bir sahte/fake yazılabilir,
  tıpkı IVehicleRepository gibi). Reflection gerektirmez.
- Eksi: Her handler'a elle bir kontrol satırı eklemek gerekir (biraz tekrar).

## Karar (Ön)
Arayüz yaklaşımı tercih edilecek — projenin genelinde zaten (IVehicleRepository,
IRideRepository, ICurrentUser gibi) arayüz tabanlı, açık bağımlılık deseni
kullanılıyor; attribute tabanlı, reflection'a dayanan "sihirli" bir mekanizma
bu tutarlılığı bozardı. Gerçek uygulama 68. günde (idempotency'nin dokümanda
asıl işleneceği gün) yapılacak.