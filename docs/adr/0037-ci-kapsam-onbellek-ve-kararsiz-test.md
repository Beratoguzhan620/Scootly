# ADR 0037: CI Kapsam Raporu, NuGet Onbellegi ve Kararsiz Test (101. Gun)

## Durum
Kabul edildi.

## Karar
CI'a kapsam raporu (coverlet + ReportGenerator 5.5.11, Summary ve artifact), NuGet onbellegi, `feature/**` push
tetikleyicisi, ayni dala art arda push'ta onceki calismayi iptal eden `concurrency` grubu ve Mvc imaji (docker isi)
eklendi. Docker isinde Mvc imajinda Leaflet ve SignalR istemcisinin varligi dogrulanir. Calistiricilar `ubuntu-24.04`'e
sabitlendi. Kapsam raporu EF migration siniflarini ve test assembly'lerini disarida birakir.

## Olculenler
- 231 test GitHub'in Ubuntu 24.04 calistiricisinda (imaj 20260927.320.1) gecti: Application 42, Architecture 7,
  Domain 84, Infrastructure 41, Concurrency 10, Api.IntegrationTests 47. Testcontainers testleri CI'da calisti.
- Kapsam: %73,7 satir, %60,6 dal (5 assembly). Api %76,3, Application %88, Domain %86,2, Infrastructure %75,2, Worker %0.
  Mvc'nin test projesi olmadigi icin olculmuyor.
- Kapsami %0 olan siniflar: Worker'daki tum is siniflari, FleetHub, SignalRFleetNotifier, RideChargeConsumer,
  VehicleStatusNotificationConsumer, OutboxPublisherService, OutboxMetricsCollector, RedisCacheService,
  RabbitMqHealthCheck, UpdateVehicleDetailsCommand ve handler'i.
- NuGet onbellegi ayni dalda geri yuklendi ("Cache hit"). Bir dalda yazilan onbellek baska dalin calismasinda gorulmedi
  (berat, feature dalinin onbellegini bulamadi). Is kirmizi bittiginde onbellek kaydedilmedi.
- Onbellekle sure kazanci olculemedi: Restore adimi onbelleksiz 12 sn, onbellekli 27 sn ve ~10 sn; is toplami
  onbelleksiz 1 dk 51 sn, onbellekli 2 dk 19 sn.
- Birlestirme sonrasi berat calismasinda `MessagingTests.Basarili_Mesaj_Onaylanmali_Ve_DLQya_Dusmemeli` bir kez
  zaman asimina dustu (Deliveries 10 sn icinde 1 olmadi). Ayni kodla yeniden calistirma gecti, yerelde 10/10 gecti.
  Kod okumasinda kuyruk bildirimi (QueueDeclare) ile baglama (QueueBind) arasinda bir pencere bulundu; testin bekleme
  kosulu yalnizca kuyrugun varligini yokluyordu. Bekleme, kuyrukta kayitli tuketici gorunene kadar bekleyecek sekilde
  degistirildi. Duzeltmeden sonra uc CI calismasi (PR push, PR pull_request, birlestirme sonrasi) yesil.
- Calistirici etiketi ubuntu-latest, 19 Ekim 2026'dan itibaren Ubuntu 26'ya tasinacak; actions/cache, checkout,
  setup-dotnet ve upload-artifact Node 20 uyarisi veriyor (isler yesil).

## Olculmeyenler
- Duzeltmenin yarisi kapattigi: sorun yerelde hic yeniden uretilemedi, bu yuzden yalnizca nedenin ortadan kalktigi
  beklenir, dogrulanmadi. Test tekrar duserse hipotez yanlistir.
- Mvc kapsami; onbellegin sure kazandirip kazandirmadigi; `-classfilters` migration filtresi olmadan raporun nasil
  gorundugu (Summary'de Migrations satiri yok, filtre calistigi icin mi kapsam disi oldugu icin mi ayirt edilmedi).
- `concurrency` iptalinin devreye girdigi bir durum gozlenmedi.

## Kisitlar
- `push` (feature/**) ve `pull_request` birlikte oldugu icin ayni degisiklik PR acikken iki kez calisir.
- Her dal kendi onbellegini yazar (~361 MB); depo basina 10 GB sinirina yakin degil ama dal sayisiyla artar.