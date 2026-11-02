-- Move the visible IoT navigation out of the legacy Manajemen IoT hierarchy.
--
-- The leaf records stay active because their codes are still used by the
-- existing permission checks and direct route aliases in the frontend. The
-- legacy roots and groups are disabled, while canonical leaf records are
-- detached so the old hierarchy no longer appears in menu administration.
-- Safe to run more than once.

BEGIN;

UPDATE "UtlMenu"
SET "ParentId" = NULL,
    "UpdatedBy" = 'system:environmental-monitoring-navigation',
    "UpdatedAt" = CURRENT_TIMESTAMP
WHERE "Code" IN (
    'IOT_MANAGEMENT',
    'MANAGEMEN_IOT',
    'IOT_VENDOR',
    'IOT_INTERNAL',
    'IOT_DEVELOPMENT',
    'LOGGER_LOKAL',
    'LOCAL_LOGGER',
    'DASHBOARD_IOT',
    'PETA_MONITORING',
    'PETA-MONITORING',
    'DAFTAR_PERANGKAT',
    'DAFTAR_PERANGKAT_VENDOR',
    'DAFTAR_PERANGKAT_INTERNAL',
    'MONITORING_SENSOR_VENDOR',
    'MONITORING_SENSOR',
    'RIWAYAT_DATA_SENSOR',
    'PRAKIRAAN_CUACA',
    'PRAKIRAAN-CUACA',
    'NOTIFIKASI_PERINGATAN',
    'IOT_NOTIFICATIONS'
);

UPDATE "UtlMenu"
SET "Active" = FALSE,
    "UpdatedBy" = 'system:environmental-monitoring-navigation',
    "UpdatedAt" = CURRENT_TIMESTAMP
WHERE "Code" IN (
    'IOT_MANAGEMENT',
    'MANAGEMEN_IOT',
    'IOT_VENDOR',
    'IOT_INTERNAL',
    'IOT_DEVELOPMENT',
    'LOGGER_LOKAL',
    'LOCAL_LOGGER',
    'PRAKIRAAN_CUACA',
    'PRAKIRAAN-CUACA',
    'NOTIFIKASI_PERINGATAN',
    'IOT_NOTIFICATIONS'
);

COMMIT;

-- Verification: no active menu should still point at a legacy IoT parent.
SELECT child."Code", child."Name", child."Route", child."Active", child."ParentId"
FROM "UtlMenu" AS child
WHERE child."Code" IN (
    'DASHBOARD_IOT',
    'PETA-MONITORING',
    'DAFTAR_PERANGKAT',
    'DAFTAR_PERANGKAT_VENDOR',
    'MONITORING_SENSOR_VENDOR',
    'RIWAYAT_DATA_SENSOR'
)
ORDER BY child."Code";
