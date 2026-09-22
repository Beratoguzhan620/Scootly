# ADR 0018 — Doğruluk sürüm damgasında kalır, dağıtık kilit yalnızca iyileştirme

- **Durum:** Kabul edildi
- **Gün:** 50
- **Bağlam:** Faz 3, Hafta 10

## Karar

Redis tabanlı dağıtık kilit (`RedisDistributedLock`) yazıldı ama **rezervasyon
yolunda kullanılmıyor**. Çift kiralamayı engelleyen şey 38. günde eklenen
sürüm damgası olmaya devam ediyor.

## Neden

Dağıtık kilit bir **doğruluk garantisi değildir**. Kilidin süresi dolduğunda
sahibi hâlâ çalışıyor olabilir: uzun bir çöp toplama duraklaması, ağ
gecikmesi veya sanal makinenin askıya alınması yeter. O anda ikinci bir süreç
kilidi alır ve iki süreç aynı anda "kilit bende" sanır.

Tek düğümlü bir Redis'te bu teorik değil, gözlenmiş bir durumdur. Redis'in
kendisi çökerse (veya bir yük devretme yaşanırsa) aynı kilit iki kez
verilebilir.

Sürüm damgası farklı bir yerde duruyor: karar **veritabanının kendisinde**,
`UPDATE ... WHERE Version = @okunan` koşuluyla veriliyor. O koşul ya tutuyor
ya tutmuyor; arada bir "belki" yok.

## Karşılaştırma (50. günün ölçümü)

Aynı 50 eşzamanlı rezervasyon denemesi iki yöntemle:

| Yöntem | Başarılı | Not |
|---|---|---|
| Koruma yok | 1'den fazla | 36. günün ölçümü — hata burada |
| Sürüm damgası | tam olarak 1 | Veritabanı kararı verir |
| Dağıtık kilit | genellikle 1 | Redis çökerse veya kilit süresi dolarsa garanti yok |

"Genellikle 1" ile "tam olarak 1" arasındaki fark, bu sistemde iki kişinin
aynı scooter'ı kiralayıp kiralayamayacağı demek.

## Kilit ne için var

Kilit **gereksiz işi** azaltmak için uygun: aynı anda iki API kopyasının aynı
raporu üretmesi, aynı toplu işi iki kez çalıştırması gibi. Orada en kötü
sonuç biraz fazladan CPU; burada en kötü sonuç bir müşteri şikayeti.

Bugün kullanılmıyor ama depoda duruyor, çünkü 54. günün arka plan servisleri
iki kopya çalıştırıldığında (yatay ölçekleme, 20. hafta) tam olarak bu
sınıfa ihtiyaç olacak: "bu turu yalnızca bir kopya çalıştırsın."

## Uygulama ayrıntıları

İki şey önemli ve ikisi de kodda yorumlanmış:

1. **Jeton.** Kilit rastgele bir değerle alınıyor. Bırakırken jeton
   karşılaştırılmasaydı, süresi dolmuş bir kilidin eski sahibi, araya giren
   yeni sahibin kilidini silerdi.
2. **Lua betiği.** Karşılaştırma ve silme tek bir betikte, çünkü "oku, eşitse
   sil" iki ayrı komut olsaydı arasında kilit el değiştirebilirdi — 36. günde
   ölçtüğümüz oku-kontrol-yaz yarışının aynısı, bu sefer Redis'te.

## Sonuç

Doğruluk veritabanında, iyileştirme Redis'te. İkisini karıştırmak, bu ADR'nin
engellemeye çalıştığı hatanın kendisi.
