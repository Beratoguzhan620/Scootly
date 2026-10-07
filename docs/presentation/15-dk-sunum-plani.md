# Scootly: 15 dakikalik sunum plani

Sure dagilimi bir plandir; kronometreyle prova edilmedi (110. gun). Provada sureleri kendi ritminize gore guncelleyin.

| Dk | Bolum | Ne anlatilir | Kaynak |
|---|---|---|---|
| 0-1 | Problem | Sehir ici scooter paylasimi: rezerve et, surus, ode. Capstone olarak neden secildi. | README Genel Bakis |
| 1-3 | Mimari | Clean Architecture katmanlari, 5 bounded context, calisan surecler (Api, Mvc, Worker, simulatorler). | system-design.md bolum 1 |
| 3-8 | Bir surusun yolculugu (ana bolum) | Rezerve -> baslat -> bitir (202) -> outbox -> RabbitMQ -> odeme -> webhook -> uzlastirma. "Neden 202?", "tekrar teslim neden guvenli?" | system-design.md bolum 2 |
| 8-10 | Demo (kisa) | Kayitli bir sureci Swagger/Mvc'den bitir, `payment-status` ile durumu goster. Yedek: ekran goruntusu. | README Calistirma |
| 10-12 | Dayaniklilik | 106. gun provasi: RabbitMQ kesintisi, bulunan hata, 109. gun duzeltmesi ve testi. | ADR 0042, 0044; RabbitMqRecoveryTests |
| 12-14 | Riskler ve borc | En onemli 4 risk: tek PostgreSQL, DLQ alarmi yok, surec ici telemetri kuyrugu, prod'da gozlemlenebilirlik yok. Olculenler ile olculmeyenleri ayir. | system-design.md bolum 3 |
| 14-15 | Kapanis | Ne ogrendim (retro "Kisisel" bolumunden), sonraki adim. | ADR 0044 |

## Prova listesi

1. Bir kez sesli, kronometreyle bastan sona calis; her bolumun gercek suresini yaz.
2. Demo icin yigin onceden ayakta olsun (dev stack bu oturumda durduruldu, volume'lar duruyor; calistirma komutlari README'de). Calismazsa yedek ekran goruntusune gec.
3. Sorulari yuksek sesle cevapla:
   - "Neden mikroservis degil?" (ADR 0017 modular monolit degerlendirmesi)
   - "Mesaj iki kez gelirse ne olur?" (bolum 2, sondaki "tekrar teslim neden guvenli")
   - "Odeme saglayicisi cokerse?" (devre kesici, retry/DLQ, Worker uzlastirma; 106. gun olculdu)
   - "Neyi olcmediniz?" (system-design.md bolum 5; durustce)
4. Slayt gerekmiyorsa bu tablo konusma notudur; slayt yapilacaksa Gun 110 sonrasi ayri talep edin.
