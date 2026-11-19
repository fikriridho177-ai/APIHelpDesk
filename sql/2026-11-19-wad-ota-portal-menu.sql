-- Portal OTA — sub-menu seed (UtlMenu)
-- Target database : helpdeskasd (PostgreSQL).
-- Idempotent, aman dijalankan ulang.
--
-- Struktur akhir di bawah grup WAD:
--   Portal OTA & Perangkat            (parent, tanpa route)
--     ├── WAD_ECS_OTA_FIRMWARE        /ecs/ota
--     └── WAD_ECS_OTA_DEVICES         /ecs/ota/perangkat
--
-- Hak akses (UtlGroupMenu) disalin dari menu induk
-- WAD_ECS_DEVICE_MANAGEMENT agar grup yang sudah punya akses
-- Manajemen Perangkat & OTA otomatis mendapat kedua sub-halaman.
-- SuperAdmin tetap melihat seluruh menu aktif.

BEGIN;

-- 1. Menu lama Manajemen Perangkat & OTA menjadi induk tanpa route.
UPDATE "UtlMenu"
SET "Name" = 'Portal OTA & Perangkat',
    "Route" = NULL,
    "Icon" = 'settings',
    "UpdatedAt" = now(),
    "UpdatedBy" = 'system'
WHERE "Code" = 'WAD_ECS_DEVICE_MANAGEMENT'
  AND (
      "Name" <> 'Portal OTA & Perangkat'
      OR "Route" IS NOT NULL
  );

-- 2. Dua sub-halaman di bawah induk Portal OTA.
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT parent."Id", x."Code", x."Name",
       CASE
           WHEN NOT EXISTS (
               SELECT 1 FROM "UtlMenu" o
               WHERE o."Route" = x."Route" AND o."Active" AND o."Code" <> x."Code"
           )
           THEN x."Route"
           ELSE NULL
       END,
       x."Icon", x."Description", 'MENU', x."SortOrder", true, 'system', now()
FROM "UtlMenu" parent
CROSS JOIN (VALUES
    ('WAD_ECS_OTA_FIRMWARE', 'Firmware & OTA',       '/ecs/ota',            'upload', 'Upload firmware, pilih perangkat, dan pantau penugasan OTA.', 1),
    ('WAD_ECS_OTA_DEVICES',  'Manajemen Perangkat',  '/ecs/ota/perangkat',  'chip',   'Registry perangkat, sensor & pin, konfigurasi, firmware, dan diagnostik.', 2)
) AS x("Code", "Name", "Route", "Icon", "Description", "SortOrder")
WHERE parent."Code" = 'WAD_ECS_DEVICE_MANAGEMENT'
  AND NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = x."Code");

-- 3. Salin hak akses grup dari menu induk ke sub-halaman baru.
INSERT INTO "UtlGroupMenu" ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT r."GroupId", child."Id",
       r."CanView", r."CanRead", r."CanCreate", r."CanUpdate", r."CanDelete", r."CanApprove", r."CanExport"
FROM "UtlGroupMenu" r
JOIN "UtlMenu" parent ON parent."Id" = r."MenuId"
JOIN "UtlMenu" child ON child."ParentId" = parent."Id"
WHERE parent."Code" = 'WAD_ECS_DEVICE_MANAGEMENT'
  AND child."Code" IN ('WAD_ECS_OTA_FIRMWARE', 'WAD_ECS_OTA_DEVICES')
  AND NOT EXISTS (
      SELECT 1 FROM "UtlGroupMenu" x
      WHERE x."GroupId" = r."GroupId" AND x."MenuId" = child."Id"
  );

COMMIT;
