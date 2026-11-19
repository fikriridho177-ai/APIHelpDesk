-- ============================================================
-- Bersihkan perangkat uji/dummy IoT (wad)
-- Target: device_id 'lk' (junk) dan 'DL-003' (DataLogger ESP32 Test 001)
-- Keduanya sudah Disabled dan kredensialnya sudah dicabut.
--
-- JANGAN jalankan blok DELETE sebelum menyetujui hasil dry-run.
-- Cara pakai:
--   1. Jalankan hanya bagian "LANGKAH 1" -> periksa hasil.
--   2. Jalankan seluruh file dalam satu transaksi (LANGKAH 2-5).
--   3. Jika hasil salah, jalankan ROLLBACK, bukan COMMIT.
-- Rollback setelah COMMIT ada di bagian akhir.
-- ============================================================


-- ------------------------------------------------------------
-- LANGKAH 1 - DRY RUN (read only)
-- ------------------------------------------------------------
SELECT a."DeviceId",
       a."Name",
       a."Status",
       a."IsActive",
       a."CreatedAt",
       a."LastSeenAt",
       (SELECT count(*) FROM "IotDeviceCredential" c WHERE c."DeviceAgentId" = a."Id") AS credentials,
       (SELECT count(*) FROM "IotDeviceReport" r WHERE r."DeviceAgentId" = a."Id") AS reports,
       (SELECT count(*) FROM "IotDeviceSensor" s WHERE s."DeviceAgentId" = a."Id") AS sensors,
       (SELECT count(*) FROM "IotOtaJob" j WHERE j."DeviceAgentId" = a."Id") AS ota_jobs,
       (SELECT count(*) FROM "IotActivityEvent" e WHERE e."DeviceAgentId" = a."Id") AS activity_events
FROM "IotDeviceAgent" a
WHERE a."DeviceId" IN ('lk', 'DL-003')
ORDER BY a."DeviceId";


-- ------------------------------------------------------------
-- LANGKAH 2 - GUARD + BACKUP
-- ------------------------------------------------------------
BEGIN;

DO $$
DECLARE risky int;
BEGIN
    SELECT count(*) INTO risky
    FROM "IotDeviceAgent" a
    WHERE a."DeviceId" IN ('lk', 'DL-003')
      AND (
          a."IsActive"
          OR EXISTS (
              SELECT 1 FROM "IotDeviceCredential" c
              WHERE c."DeviceAgentId" = a."Id"
                AND c."RevokedAt" IS NULL
          )
      );

    IF risky > 0 THEN
        RAISE EXCEPTION
            'Dibatalkan: masih ada perangkat target yang aktif atau punya device key aktif. Nonaktifkan lewat halaman Perangkat lebih dulu.';
    END IF;
END $$;

CREATE TABLE IF NOT EXISTS "IotCleanupBackup_20261119_Device" AS
SELECT * FROM "IotDeviceAgent" WHERE "DeviceId" IN ('lk', 'DL-003');

CREATE TABLE IF NOT EXISTS "IotCleanupBackup_20261119_Credential" AS
SELECT c.* FROM "IotDeviceCredential" c
WHERE c."DeviceAgentId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_Device");

CREATE TABLE IF NOT EXISTS "IotCleanupBackup_20261119_Report" AS
SELECT r.* FROM "IotDeviceReport" r
WHERE r."DeviceAgentId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_Device");

CREATE TABLE IF NOT EXISTS "IotCleanupBackup_20261119_Sensor" AS
SELECT s.* FROM "IotDeviceSensor" s
WHERE s."DeviceAgentId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_Device");

CREATE TABLE IF NOT EXISTS "IotCleanupBackup_20261119_Configuration" AS
SELECT cfg.* FROM "IotDeviceConfiguration" cfg
WHERE cfg."DeviceAgentId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_Device");

CREATE TABLE IF NOT EXISTS "IotCleanupBackup_20261119_OtaJob" AS
SELECT j.* FROM "IotOtaJob" j
WHERE j."DeviceAgentId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_Device");

CREATE TABLE IF NOT EXISTS "IotCleanupBackup_20261119_OtaEvent" AS
SELECT e.* FROM "IotOtaEvent" e
WHERE e."OtaJobId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_OtaJob");

CREATE TABLE IF NOT EXISTS "IotCleanupBackup_20261119_Activity" AS
SELECT e.* FROM "IotActivityEvent" e
WHERE e."DeviceAgentId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_Device");


-- ------------------------------------------------------------
-- LANGKAH 3 - DELETE (anak dulu, lalu perangkat)
-- ------------------------------------------------------------
DELETE FROM "IotActivityEvent"
WHERE "DeviceAgentId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_Device");

DELETE FROM "IotOtaEvent"
WHERE "OtaJobId" IN (SELECT "Id" FROM "IotCleanupBackup_20261119_OtaJob");

DELETE FROM "IotDeviceAgent"
WHERE "DeviceId" IN ('lk', 'DL-003');

-- Sisanya (credential, report, sensor, configuration, ota job) ikut
-- terhapus oleh ON DELETE CASCADE.


-- ------------------------------------------------------------
-- LANGKAH 4 - VERIFIKASI
-- ------------------------------------------------------------
SELECT count(*) AS sisa_device_target
FROM "IotDeviceAgent"
WHERE "DeviceId" IN ('lk', 'DL-003');

-- Lanjutkan dengan COMMIT hanya jika hasil di atas 0.
COMMIT;
-- ROLLBACK;


-- ------------------------------------------------------------
-- ROLLBACK SETELAH COMMIT (bila perlu memulihkan)
-- ------------------------------------------------------------
-- BEGIN;
-- INSERT INTO "IotDeviceAgent" SELECT * FROM "IotCleanupBackup_20261119_Device";
-- INSERT INTO "IotDeviceCredential" SELECT * FROM "IotCleanupBackup_20261119_Credential";
-- INSERT INTO "IotDeviceReport" SELECT * FROM "IotCleanupBackup_20261119_Report";
-- INSERT INTO "IotDeviceSensor" SELECT * FROM "IotCleanupBackup_20261119_Sensor";
-- INSERT INTO "IotDeviceConfiguration" SELECT * FROM "IotCleanupBackup_20261119_Configuration";
-- INSERT INTO "IotOtaJob" SELECT * FROM "IotCleanupBackup_20261119_OtaJob";
-- INSERT INTO "IotOtaEvent" SELECT * FROM "IotCleanupBackup_20261119_OtaEvent";
-- INSERT INTO "IotActivityEvent" SELECT * FROM "IotCleanupBackup_20261119_Activity";
-- COMMIT;
