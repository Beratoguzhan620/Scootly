# ADR 0014: Paralel İşleme Deneyi — Beklenmedik Sonuç

## Durum
Gözlem kaydedildi.

## Bağlam
58. günde, 10.000 kayıtlık bir CPU-yoğun işlemi sıralı (foreach) ve paralel
(Parallel.ForEach) olarak, üç farklı parça boyutunda (10, 100, 1000) karşılaştırdık.

## Ölçüm Sonucu

| Parça Boyutu | Sıralı | Paralel | Kazanç |
|---|---|---|---|
| 10 | 580 ms | 53 ms | ~11x |
| 100 | 579 ms | 49 ms | ~12x |
| 1000 | 582 ms | 74 ms | ~8x |

## Gözlem — Dokümanın Varsayımıyla Çelişki
Genel beklenti, küçük parça boyutlarında paralelliğin görev başlatma ek yükü
(overhead) yüzünden sıralıdan daha yavaş olabileceğiydi. Bu makinede (çok
çekirdekli CPU) bu senaryo GÖRÜLMEDİ — hatta en küçük parça boyutunda (10)
bile paralel, sıralıdan ~11 kat hızlı çıktı.

## Ders
"Paralellik küçük parçalarda yavaştır" genel kuralı, her donanımda ve her iş
yükünde geçerli değil — bu, CPU çekirdek sayısına, işin gerçek ağırlığına ve
.NET'in Parallel.ForEach'in kendi iç parçalama (partitioning) mantığına bağlı.
Bu deney, "asla varsayma, her zaman ölç" prensibinin, beklenen sonucu
DOĞRULAMASA bile değerli olduğunun kanıtı — sonucun kendisi kadar, sonucu
üreten disiplin de önemli.