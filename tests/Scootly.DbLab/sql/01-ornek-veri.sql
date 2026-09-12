-- Gün 31 — 100 bin satırlık örnek veri
--
-- Uygulama koduna hiç dokunmadan, doğrudan SQL ile üretiliyor. Amaç, indeks ve
-- plan çalışmalarının (33.-34. gün) anlamlı olacağı büyüklükte bir tablo elde
-- etmek: 20 satırlık bir tabloda PostgreSQL indeksleri zaten görmezden gelir,
-- çünkü tabloyu baştan sona taramak indeksi okumaktan ucuzdur.
--
-- generate_series ile üretiliyor; tek tek INSERT yerine tek bir komut olması,
-- 100 bin satırı saniyeler içinde yazmayı sağlıyor.

TRUNCATE TABLE "Rides", "Vehicles" CASCADE;

-- Adana merkez çevresinde rastgele konumlar.
-- Enlem 36.90-37.10, boylam 35.20-35.45 aralığı.
INSERT INTO "Vehicles" ("Id", "Status", "Brand", "RangeKm", "BatteryPercentage", "Latitude", "Longitude")
SELECT
    gen_random_uuid(),
    (ARRAY['Available','Reserved','InRide','Maintenance'])[1 + (random() * 3)::int],
    (ARRAY['Segway Ninebot','Xiaomi Mi','Okai ES','Yadea KS'])[1 + (random() * 3)::int],
    30 + (random() * 40)::int,
    (random() * 100)::int,
    36.90 + random() * 0.20,
    35.20 + random() * 0.25
FROM generate_series(1, 100000);

-- Her araç için ortalama bir sürüş. Yarısı tamamlanmış, yarısı aktif.
-- Başlangıç zamanları son 30 güne yayılıyor ki "son 24 saat" sorgusu
-- anlamlı bir alt küme döndürsün.
INSERT INTO "Rides" (
    "Id", "DriverId", "VehicleId", "Status",
    "StartLatitude", "StartLongitude", "EndLatitude", "EndLongitude",
    "StartedAt", "EndedAt", "Fare")
SELECT
    gen_random_uuid(),
    gen_random_uuid(),
    v."Id",
    CASE WHEN random() < 0.5 THEN 'Completed' ELSE 'Active' END,
    v."Latitude",
    v."Longitude",
    CASE WHEN random() < 0.5 THEN v."Latitude" + (random() - 0.5) * 0.02 END,
    CASE WHEN random() < 0.5 THEN v."Longitude" + (random() - 0.5) * 0.02 END,
    now() - (random() * interval '30 days'),
    CASE WHEN random() < 0.5 THEN now() - (random() * interval '29 days') END,
    CASE WHEN random() < 0.5 THEN (5 + random() * 45)::numeric(10,2) END
FROM "Vehicles" v;

-- PostgreSQL'in planlayıcısı tablo istatistiklerine bakarak karar verir.
-- Toplu yüklemeden hemen sonra bu istatistikler eski olur ve planlayıcı
-- tablonun hâlâ boş olduğunu sanarak yanlış plan seçer. 34. günde
-- "tahmini satır sayısı ile gerçek satır sayısı" farkına bakacağız;
-- bu komut olmadan o fark ölçümün kendisinden değil, ihmalden gelirdi.
ANALYZE "Vehicles";
ANALYZE "Rides";
