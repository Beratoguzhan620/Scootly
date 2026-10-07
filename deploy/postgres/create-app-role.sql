-- Uygulamaların (Api, Worker, Mvc) bağlandığı, yalnızca veri okuyup yazabilen rol.
-- Şema değişikliği (migration) süper kullanıcıyla yapılır; uygulama rolü tablo oluşturamaz, silemez, rol yönetemez.
-- İdempotenttir: her "up"ta çalışır, parolayı .env'deki değere eşitler ve yeni tablolara da yetki verir.
-- Değişkenler psql -v ile gelir: app_user, app_password, db_name.

\set ON_ERROR_STOP on

SELECT format('CREATE ROLE %I LOGIN', :'app_user')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = :'app_user') \gexec

ALTER ROLE :"app_user" WITH LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS PASSWORD :'app_password';

GRANT CONNECT ON DATABASE :"db_name" TO :"app_user";
GRANT USAGE ON SCHEMA public TO :"app_user";

GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO :"app_user";
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO :"app_user";

-- Migration'ın ileride oluşturacağı tablolar için de (bu betiği çalıştıran rolün oluşturdukları).
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO :"app_user";
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO :"app_user";

-- Migration geçmişi yalnızca okunabilir kalsın.
REVOKE INSERT, UPDATE, DELETE ON TABLE "__EFMigrationsHistory" FROM :"app_user";
