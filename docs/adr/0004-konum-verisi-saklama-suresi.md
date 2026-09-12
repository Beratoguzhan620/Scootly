# ADR 0004: Konum Verisi Saklama Süresi

## Durum
Kabul edildi.

## Bağlam
Sürüş kayıtları (Ride) başlangıç ve bitiş konumlarını (GeoPoint) kalıcı olarak saklıyor.
Konum verisi, kullanıcıların hareket geçmişini ortaya çıkarabilecek hassas bir bilgidir.
Süresiz saklamak hem gizlilik riski hem de gereksiz veri birikimi anlamına gelir.

## Karar
Tamamlanmış sürüşlerin konum verisi (StartLocation, EndLocation) 90 gün süreyle saklanır.
Bu sürenin sonunda, sürüş kaydının kendisi silinmez ama konum alanları anonimleştirilir
(null'a çekilir) — süre, ücret, durum gibi istatistiksel bilgiler kalır.

## Alternatif: Süresiz saklama
Tüm sürüş verisini hiç silmeden, analiz ve raporlama için sonsuza kadar tutmak.

## Neden Seçilmedi
Süresiz saklama, KVKK/GDPR gibi veri koruma prensipleriyle ("gereğinden fazla veri
tutma") çelişir ve kullanıcıların hareket geçmişinin uzun vadede ifşa riskini artırır.
90 günlük bir pencere, hem operasyonel ihtiyaçları (anlaşmazlık çözümü, destek talepleri)
karşılamak hem de gizlilik riskini sınırlamak arasında bir denge sağlar.

## Uygulama Notu
Bu kararın gerçek uygulaması (otomatik anonimleştirme işi) henüz yazılmadı — bu,
ileride bir arka plan servisi (BackgroundService) olarak eklenecek. Şimdilik yalnızca
karar kayıt altına alınmıştır.