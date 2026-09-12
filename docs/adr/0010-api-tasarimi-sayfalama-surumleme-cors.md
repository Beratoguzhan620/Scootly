# 0010 — API tasarımı: sayfalama, sürümleme, CORS

- **Durum:** Kabul edildi
- **Tarih:** 29.–30. gün

## Karar 1 — Sayfa numarası tabanlı sayfalama, üst sınırlı

`PageRequest` (`PageNumber`, `PageSize`), varsayılan 20, **üst sınır 100**.

**Neden üst sınır:** Bu yalnızca bir performans ayarı değil, bir güvenlik sınırı.
Sınır olmasaydı bir istemci `?pageSize=1000000` yazarak sunucuyu tek istekte tüm
tabloyu belleğe almaya zorlayabilirdi. `GET /api/vehicles` kimlik doğrulaması
gerektirmiyor, yani bunun saldırgana maliyeti sıfır.

**Neden sayfa numarası, imleç (cursor) değil:** Sayfa numarası basit ve
istemcinin "kaç sayfa var" sorusunu cevaplayabiliyor. Bedeli, veri değişirken
tutarsızlık (araya yeni kayıt girerse bir kayıt iki sayfada birden çıkabilir) ve
her istekte ikinci bir `COUNT` sorgusu. Tablo büyüdüğünde `COUNT`'un maliyeti
sayfanın kendisinden yüksek olabilir; Faz 3'te ölçülecek ve gerekirse imleç
tabanlıya geçilecek.

**`OrderBy` zorunlu.** Sıralama verilmezse PostgreSQL satırları istediği düzende
döndürebilir; aynı sorgu iki kez çalıştığında farklı sıra gelirse bazı kayıtlar
iki sayfada birden çıkar, bazıları hiç çıkmaz. Bu hata veri az olduğu sürece
fark edilmez — yani üretimde ortaya çıkar.

## Karar 2 — Sürümleme yol üzerinden, kütüphanesiz

`/api/v1/vehicles`. Eski sürümsüz yol (`/api/vehicles`) şimdilik geçerli kalıyor.

**Neden kütüphanesiz:** `Asp.Versioning` paketi başlık, sorgu dizesi ve medya
tipi üzerinden sürüm pazarlığı da sunuyor. Bize bunların hiçbiri gerekmiyor;
tek istemcimiz (18. haftada gelecek MVC arayüzü) URL yazacak. Kullanılmayan bir
soyutlama, bakım yükü ve okuyanın öğrenmesi gereken fazladan kavram demektir.
Gerekirse sonradan eklenebilir; erken eklenmiş bir bağımlılığı çıkarmak daha zor.

**Neden eski yol duruyor:** Kırıcı değişikliği tek adımda yapmak yerine önce
uyarmak, sonra kaldırmak doğru sıra. Kaldırma teknik borç listesinde.

## Karar 3 — CORS listesi yapılandırmadan, `AllowAnyOrigin` yok

İzinli kaynaklar `Cors:AllowedOrigins` altından geliyor; varsayılan **boş liste**,
yani hiçbir çapraz kaynak isteği geçmiyor.

**Neden:** `AllowAnyOrigin`, herhangi bir sayfanın bu API'ye tarayıcıdan istek
yapabilmesi demektir. Bugün token `Authorization` başlığıyla taşındığı için risk
sınırlı; ama 18. haftada tarayıcı arayüzü bağlandığında ve 19. haftada çerez
tabanlı kimlik eklendiğinde aynı ayar doğrudan bir açığa dönüşür. Boş liste ile
başlamak, o gün birinin bunu fark etmesini gerektirmiyor.

Yöntemler `GET` ve `POST` ile, başlıklar `Authorization` ve `Content-Type` ile
sınırlı. Genişletmek gerektiğinde tek satır; daraltmak ise ancak birinin fark
etmesiyle mümkün olurdu.
