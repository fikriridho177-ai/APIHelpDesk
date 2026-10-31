-- Navigasi Manajemen IoT untuk peta monitoring dan history sensor.
-- PostgreSQL; aman dijalankan ulang. Kode menu lama tidak dihapus.

BEGIN;

UPDATE "UtlMenu"
SET "Name" = 'Manajemen IoT',
    "Route" = '/iot-management',
    "MenuType" = 'GROUP',
    "Active" = TRUE,
    "UpdatedBy" = 'system:iot-monitoring-map',
    "UpdatedAt" = CURRENT_TIMESTAMP
WHERE "Code" = 'MANAGEMEN_IOT';

UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Dashboard',
    "Route" = '/iot-management/dashboard-iot',
    "MenuType" = 'MENU',
    "SortOrder" = 1,
    "Active" = TRUE,
    "UpdatedBy" = 'system:iot-monitoring-map',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'DASHBOARD_IOT'
  AND parent."Code" = 'MANAGEMEN_IOT';

INSERT INTO "UtlMenu" (
    "ParentId", "Code", "Name", "Route", "Icon", "MenuType",
    "SortOrder", "Active", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
)
SELECT parent."Id", 'PETA-MONITORING', 'Peta Monitoring',
       '/iot-management/peta-monitoring', 'map', 'MENU', 2, TRUE,
       'system:iot-monitoring-map', CURRENT_TIMESTAMP,
       'system:iot-monitoring-map', CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE parent."Code" = 'MANAGEMEN_IOT'
  AND NOT EXISTS (
      SELECT 1 FROM "UtlMenu" WHERE "Code" = 'PETA-MONITORING'
  );

UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Peta Monitoring',
    "Route" = '/iot-management/peta-monitoring',
    "MenuType" = 'MENU',
    "SortOrder" = 2,
    "Active" = TRUE,
    "UpdatedBy" = 'system:iot-monitoring-map',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'PETA-MONITORING'
  AND parent."Code" = 'MANAGEMEN_IOT';

UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Manajemen Perangkat',
    "Route" = '/iot-management/daftar-perangkat',
    "MenuType" = 'MENU',
    "SortOrder" = 3,
    "Active" = TRUE,
    "UpdatedBy" = 'system:iot-monitoring-map',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'DAFTAR_PERANGKAT'
  AND parent."Code" = 'MANAGEMEN_IOT';

UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Monitoring Sensor',
    "Route" = '/iot-management/monitoring-sensor',
    "MenuType" = 'MENU',
    "SortOrder" = 4,
    "Active" = TRUE,
    "UpdatedBy" = 'system:iot-monitoring-map',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'MONITORING_SENSOR_VENDOR'
  AND parent."Code" = 'MANAGEMEN_IOT';

INSERT INTO "UtlMenu" (
    "ParentId", "Code", "Name", "Route", "Icon", "MenuType",
    "SortOrder", "Active", "CreatedBy", "CreatedAt", "UpdatedBy", "UpdatedAt"
)
SELECT parent."Id", 'RIWAYAT_DATA_SENSOR', 'Riwayat Data Sensor',
       '/iot-management/riwayat-data-sensor', 'history', 'MENU', 5, TRUE,
       'system:iot-monitoring-map', CURRENT_TIMESTAMP,
       'system:iot-monitoring-map', CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE parent."Code" = 'MANAGEMEN_IOT'
  AND NOT EXISTS (
      SELECT 1 FROM "UtlMenu" WHERE "Code" = 'RIWAYAT_DATA_SENSOR'
  );

UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Riwayat Data Sensor',
    "Route" = '/iot-management/riwayat-data-sensor',
    "MenuType" = 'MENU',
    "SortOrder" = 5,
    "Active" = TRUE,
    "UpdatedBy" = 'system:iot-monitoring-map',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'RIWAYAT_DATA_SENSOR'
  AND parent."Code" = 'MANAGEMEN_IOT';

-- Weather tetap dapat dibuka lewat bookmark/route langsung, tetapi tidak lagi
-- menjadi item utama karena cuaca sudah menjadi konteks detail perangkat.
UPDATE "UtlMenu"
SET "Active" = FALSE,
    "UpdatedBy" = 'system:iot-monitoring-map',
    "UpdatedAt" = CURRENT_TIMESTAMP
WHERE "Code" IN (
    'PRAKIRAAN-CUACA', 'PRAKIRAAN_CUACA',
    'NOTIFIKASI_PERINGATAN', 'IOT_NOTIFICATIONS'
);

-- Kode lama tetap ada untuk permission dan resolver route, tetapi tidak tampil
-- sebagai pemisahan Vendor/Internal di navigasi baru.
UPDATE "UtlMenu"
SET "Active" = FALSE,
    "UpdatedBy" = 'system:iot-monitoring-map',
    "UpdatedAt" = CURRENT_TIMESTAMP
WHERE "Code" IN (
    'IOT_VENDOR', 'IOT_DEVELOPMENT', 'DAFTAR_PERANGKAT_INTERNAL',
    'MONITORING_SENSOR'
);

COMMIT;

SELECT "Code", "Name", "Route", "MenuType", "SortOrder", "Active"
FROM "UtlMenu"
WHERE "Code" IN (
    'MANAGEMEN_IOT', 'DASHBOARD_IOT', 'PETA-MONITORING',
    'DAFTAR_PERANGKAT', 'MONITORING_SENSOR_VENDOR',
    'RIWAYAT_DATA_SENSOR', 'PRAKIRAAN-CUACA'
)
ORDER BY "SortOrder", "Code";
