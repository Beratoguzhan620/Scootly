-- Gün 31 — On doğrulama sorgusu
--
-- Her sorgu tek satırda "-- ADI: ..." yorumuyla başlar; ölçüm projesi
-- bu yorumu başlık olarak kullanıp sorguları tek tek çalıştırır.
-- Sorgular noktalı virgülle ayrılır.

-- ADI: 1. Durumlara göre araç sayısı
SELECT "Status", COUNT(*) AS adet
FROM "Vehicles"
GROUP BY "Status"
ORDER BY adet DESC;

-- ADI: 2. Son 24 saatte tamamlanan sürüş sayısı
SELECT COUNT(*) AS tamamlanan
FROM "Rides"
WHERE "Status" = 'Completed'
  AND "EndedAt" >= now() - interval '24 hours';

-- ADI: 3. Yakındaki müsait araçlar (merkez 36.99, 35.33 — kaba kutu filtresi)
SELECT COUNT(*) AS yakinda_musait
FROM "Vehicles"
WHERE "Status" = 'Available'
  AND "Latitude"  BETWEEN 36.98 AND 37.00
  AND "Longitude" BETWEEN 35.32 AND 35.34;

-- ADI: 4. Marka bazında ortalama batarya
SELECT "Brand", ROUND(AVG("BatteryPercentage")::numeric, 1) AS ortalama_batarya, COUNT(*) AS adet
FROM "Vehicles"
GROUP BY "Brand"
ORDER BY ortalama_batarya DESC;

-- ADI: 5. Şarj gerektiren araçlar (batarya %20 altı ve müsait)
SELECT COUNT(*) AS sarj_gerekiyor
FROM "Vehicles"
WHERE "BatteryPercentage" < 20
  AND "Status" = 'Available';

-- ADI: 6. INNER JOIN — sürüşü olan araçlar ve sürüş sayıları (ilk 10)
SELECT v."Id", v."Brand", COUNT(r."Id") AS surus_sayisi
FROM "Vehicles" v
INNER JOIN "Rides" r ON r."VehicleId" = v."Id"
GROUP BY v."Id", v."Brand"
ORDER BY surus_sayisi DESC, v."Id"
LIMIT 10;

-- ADI: 7. LEFT JOIN — hic surusu olmayan arac sayisi
-- INNER JOIN ile fark buradadir: LEFT JOIN eslesmeyen sol satirlari da getirir,
-- boylece "hic surusu olmayan" sorusu cevaplanabilir hale gelir.
SELECT COUNT(*) AS hic_kullanilmamis
FROM "Vehicles" v
LEFT JOIN "Rides" r ON r."VehicleId" = v."Id"
WHERE r."Id" IS NULL;

-- ADI: 8. Gunluk tamamlanan surus ve ciro (son 7 gun)
SELECT DATE("EndedAt") AS gun,
       COUNT(*) AS surus,
       ROUND(SUM("Fare")::numeric, 2) AS ciro
FROM "Rides"
WHERE "Status" = 'Completed' AND "EndedAt" >= now() - interval '7 days'
GROUP BY DATE("EndedAt")
ORDER BY gun DESC;

-- ADI: 9. Alt sorgu — ortalamanin uzerinde ucret odenen surus sayisi
SELECT COUNT(*) AS ortalama_ustu
FROM "Rides"
WHERE "Fare" > (SELECT AVG("Fare") FROM "Rides" WHERE "Fare" IS NOT NULL);

-- ADI: 10. Ayni sorgunun JOIN hali — planlayici alt sorguyu genelde ayni
-- bicimde optimize eder; 34. gunde iki planin ayni cikip cikmadigina bakacagiz.
SELECT COUNT(*) AS ortalama_ustu
FROM "Rides" r
CROSS JOIN (SELECT AVG("Fare") AS ort FROM "Rides" WHERE "Fare" IS NOT NULL) o
WHERE r."Fare" > o.ort;
