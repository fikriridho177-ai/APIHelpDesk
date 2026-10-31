-- Rapikan navigasi IoT tanpa mengubah kode menu yang sudah dipakai permission.
-- Jalankan sekali pada database HelpDesk PostgreSQL.
-- Script ini tidak membuat menu baru dan aman dijalankan ulang.

BEGIN;

-- Root teknis IoT.
UPDATE "UtlMenu"
SET "ParentId" = NULL,
    "Name" = 'IoT Management',
    "Route" = '/iot-management',
    "MenuType" = 'GROUP',
    "SortOrder" = 90,
    "Active" = TRUE,
    "Icon" = COALESCE(NULLIF("Icon", ''), 'chart'),
    "UpdatedBy" = 'system:iot-menu-restructure',
    "UpdatedAt" = CURRENT_TIMESTAMP
WHERE "Code" = 'IOT_MANAGEMENT';

-- Kelompok lama tetap disimpan sebagai kompatibilitas kode menu, tetapi tidak
-- ditampilkan lagi karena Vendor dan Internal sekarang memakai satu menu.
UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Vendor Logger (legacy)',
    "Route" = '/iot-management/iot-vendor',
    "MenuType" = 'GROUP',
    "SortOrder" = 98,
    "Active" = FALSE,
    "Icon" = COALESCE(NULLIF(child."Icon", ''), 'desktop'),
    "UpdatedBy" = 'system:iot-menu-restructure',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'IOT_VENDOR'
  AND parent."Code" = 'IOT_MANAGEMENT';

UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Logger Lokal (legacy)',
    "Route" = '/iot-management/logger-lokal',
    "MenuType" = 'GROUP',
    "SortOrder" = 99,
    "Active" = FALSE,
    "Icon" = COALESCE(NULLIF(child."Icon", ''), 'settings'),
    "UpdatedBy" = 'system:iot-menu-restructure',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'IOT_DEVELOPMENT'
  AND parent."Code" = 'IOT_MANAGEMENT';

-- Menu utama monitoring: satu halaman, dengan filter sumber Vendor/Internal.
UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Monitoring Sensor',
    "Route" = '/iot-management/dashboard-iot',
    "MenuType" = 'MENU',
    "SortOrder" = 1,
    "Active" = TRUE,
    "UpdatedBy" = 'system:iot-menu-restructure',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'DASHBOARD_IOT'
  AND parent."Code" = 'IOT_MANAGEMENT';

-- Registry perangkat terpadu: kode lama dipakai ulang agar permission lama
-- tetap berlaku tanpa membuat kode menu baru.
UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = 'Daftar Perangkat',
    "Route" = '/iot-management/daftar-perangkat',
    "MenuType" = 'MENU',
    "SortOrder" = 2,
    "Active" = TRUE,
    "UpdatedBy" = 'system:iot-menu-restructure',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" = 'DAFTAR_PERANGKAT_VENDOR'
  AND parent."Code" = 'IOT_MANAGEMENT';

-- Alias menu lama disembunyikan, tetapi kode dan permission tetap dipelihara
-- agar tautan lama tidak mengubah struktur akses secara mendadak.
UPDATE "UtlMenu" AS child
SET "ParentId" = parent."Id",
    "Name" = CASE child."Code"
        WHEN 'MONITORING_SENSOR_VENDOR' THEN 'Monitoring Sensor Vendor (legacy)'
        WHEN 'DAFTAR_PERANGKAT_INTERNAL' THEN 'Daftar Logger Lokal (legacy)'
        ELSE 'Monitoring Logger Lokal (legacy)'
    END,
    "Route" = CASE child."Code"
        WHEN 'MONITORING_SENSOR_VENDOR' THEN '/iot-management/iot-vendor/monitoring-sensor-vendor'
        WHEN 'DAFTAR_PERANGKAT_INTERNAL' THEN '/iot-management/logger-lokal/daftar-perangkat-internal'
        ELSE '/iot-management/logger-lokal/monitoring-sensor'
    END,
    "MenuType" = 'MENU',
    "SortOrder" = 98,
    "Active" = FALSE,
    "UpdatedBy" = 'system:iot-menu-restructure',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE child."Code" IN (
        'MONITORING_SENSOR_VENDOR',
        'DAFTAR_PERANGKAT_INTERNAL',
        'MONITORING_SENSOR'
    )
  AND parent."Code" = 'IOT_MANAGEMENT';

-- Fitur roadmap tetap tercatat, tetapi tidak muncul sebagai link mati.
UPDATE "UtlMenu" AS m
SET "ParentId" = parent."Id",
    "Route" = CASE m."Code"
        WHEN 'FIRMWARE_REPOSITORY' THEN '/iot-management/logger-lokal/firmware-repository'
        WHEN 'OTA_UPLOAD' THEN '/iot-management/logger-lokal/ota-upload'
        WHEN 'RIWAYAT_UPDATE_FIRMWARE' THEN '/iot-management/logger-lokal/riwayat-update-firmware'
        ELSE m."Route"
    END,
    "MenuType" = 'MENU',
    "Active" = FALSE,
    "UpdatedBy" = 'system:iot-menu-restructure',
    "UpdatedAt" = CURRENT_TIMESTAMP
FROM "UtlMenu" AS parent
WHERE m."Code" IN (
        'FIRMWARE_REPOSITORY',
        'OTA_UPLOAD',
        'RIWAYAT_UPDATE_FIRMWARE'
    )
  AND parent."Code" = 'IOT_MANAGEMENT';

-- Menu IoT adalah area teknis Super Admin. Hak akses WAD tidak disentuh.
UPDATE "UtlGroupMenu" AS gm
SET "CanView" = FALSE,
    "CanRead" = FALSE,
    "CanCreate" = FALSE,
    "CanUpdate" = FALSE,
    "CanDelete" = FALSE,
    "CanApprove" = FALSE,
    "CanExport" = FALSE
FROM "UtlGroup" AS g,
     "UtlMenu" AS m
WHERE g."Id" = gm."GroupId"
  AND m."Id" = gm."MenuId"
  AND NOT g."IsSuperAdmin"
  AND m."Code" IN (
        'IOT_MANAGEMENT',
        'DASHBOARD_IOT',
        'IOT_VENDOR',
        'DAFTAR_PERANGKAT_VENDOR',
        'MONITORING_SENSOR_VENDOR',
        'IOT_DEVELOPMENT',
        'DAFTAR_PERANGKAT_INTERNAL',
        'MONITORING_SENSOR',
        'FIRMWARE_REPOSITORY',
        'OTA_UPLOAD',
        'RIWAYAT_UPDATE_FIRMWARE'
    );

COMMIT;

-- Verifikasi hasil setelah script dijalankan.
SELECT
    m."Id",
    parent."Code" AS "ParentCode",
    m."Code",
    m."Name",
    m."Route",
    m."MenuType",
    m."SortOrder",
    m."Active"
FROM "UtlMenu" AS m
LEFT JOIN "UtlMenu" AS parent
  ON parent."Id" = m."ParentId"
WHERE m."Code" IN (
    'IOT_MANAGEMENT',
    'DASHBOARD_IOT',
    'IOT_VENDOR',
    'DAFTAR_PERANGKAT_VENDOR',
    'MONITORING_SENSOR_VENDOR',
    'IOT_DEVELOPMENT',
    'DAFTAR_PERANGKAT_INTERNAL',
    'MONITORING_SENSOR',
    'FIRMWARE_REPOSITORY',
    'OTA_UPLOAD',
    'RIWAYAT_UPDATE_FIRMWARE'
)
ORDER BY COALESCE(parent."SortOrder", m."SortOrder"), m."SortOrder", m."Code";
