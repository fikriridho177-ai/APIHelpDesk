-- WAD Module — Menu seed (UtlMenu)
-- Target database : helpdeskasd (PostgreSQL).
-- Idempotent, aman dijalankan ulang. Struktur:
--   WAD (GROUP, tanpa route)
--     ├── WAD_ECS_DASHBOARD        /ecs
--     ├── WAD_ECS_STASIUN          /ecs/stasiun-monitoring
--     ├── WAD_ECS_PERANGKAT        /ecs/monitoring-perangkat
--     ├── WAD_ECS_DEVICE_MANAGEMENT /ecs/device-management
--     ├── WAD_ECS_MONITOR_CH       /ecs/monitor-curahhujan
--     ├── WAD_ECS_DATA_HISTORIS    /ecs/data-historis
--     ├── WAD_ECS_KALENDER_CH      /ecs/kalender-curah-hujan
--     ├── WAD_ECS_PETA             /ecs/peta
--     ├── WAD_ECS_CURAH_HUJAN      /ecs/curahhujan
--     ├── WAD_ECS_TMAK             /ecs/tmak
--     └── WAD_ECS_KELEMBAPAN       /ecs/kelembapan
-- Kode & route mengikuti ECS_MENUS di frontend
-- (src/features/ecs/ecsConfig.tsx). Hak default: group ADMIN mendapat
-- semua permission WAD (langsung pakai). Group lain lewat UI Hak Akses.
-- Catatan: bila route sudah dipegang menu aktif lain (misal terdaftar
-- manual sebelumnya di bawah parent lain), child tetap dibuat untuk
-- keperluan permission namun route-nya dibiarkan NULL agar tidak duplikat.

BEGIN;

-- Root grup menu WAD ( grup murni; route /ecs dipegang child dashboard ).
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT NULL, 'WAD', 'WAD', NULL, 'droplet', 'Modul Water & Device: monitoring air, perangkat IoT, dan OTA.',
       'GROUP', 110, true, 'system', now()
WHERE NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = 'WAD');

-- Anak menu (parent = root WAD). Route hanya diisi bila belum dipegang
-- menu aktif lain dengan kode berbeda.
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT parent."Id", x."Code", x."Name",
       CASE
           WHEN x."Route" IS NOT NULL
            AND NOT EXISTS (
                SELECT 1 FROM "UtlMenu" o
                WHERE o."Route" = x."Route" AND o."Active" AND o."Code" <> x."Code"
            )
           THEN x."Route"
           ELSE NULL
       END,
       COALESCE(x."Icon", 'circle'), x."Description", 'MENU', x."SortOrder", true, 'system', now()
FROM "UtlMenu" parent
CROSS JOIN (VALUES
    ('WAD_ECS_DASHBOARD',         'Pusat Layanan WAD',            '/ecs',                       'droplet',         'Hub layanan WAD: monitoring air & perangkat IoT.',                 1),
    ('WAD_ECS_STASIUN',           'Monitoring Sensor',            '/ecs/stasiun-monitoring',    'activity',        'Pantau sensor Curah Hujan, TMAK, dan TMAT.',                       2),
    ('WAD_ECS_PERANGKAT',         'Status Perangkat IoT',         '/ecs/monitoring-perangkat',  'desktop',         'Periksa koneksi dan waktu data terakhir perangkat.',               3),
    ('WAD_ECS_DEVICE_MANAGEMENT', 'Manajemen Perangkat & OTA',    '/ecs/device-management',     'settings',        'Kelola sensor, pin, konfigurasi, dan firmware (demo).',            4),
    ('WAD_ECS_MONITOR_CH',        'Monitor Curah Hujan',          '/ecs/monitor-curahhujan',    'cloud-rain',      'Monitor curah hujan.',                                             5),
    ('WAD_ECS_DATA_HISTORIS',     'Data Historis',                '/ecs/data-historis',         'database',        'Riwayat pembacaan sensor dari detail sensor.',                     6),
    ('WAD_ECS_KALENDER_CH',       'Kalender Data Sensor',         '/ecs/kalender-curah-hujan',  'calendar',        'Telusuri pembacaan harian berdasarkan parameter dan titik.',       7),
    ('WAD_ECS_PETA',              'Peta Pemantauan WAD',          '/ecs/peta',                  'map',             'Peta titik logger IoT, pengamatan manual, dan data lapangan berkoordinat.', 8),
    ('WAD_ECS_CURAH_HUJAN',       'Curah Hujan',                  '/ecs/curahhujan',            'cloud-rain',      'Data curah hujan.',                                                9),
    ('WAD_ECS_TMAK',              'Tinggi Muka Air Kanal (TMAK)', '/ecs/tmak',                  'waves',           'Tinggi muka air kanal.',                                          10),
    ('WAD_ECS_KELEMBAPAN',        'Kelembaban Tanah',             '/ecs/kelembapan',            'sprout',          'Kelembaban tanah.',                                               11)
) AS x("Code", "Name", "Route", "Icon", "Description", "SortOrder")
WHERE parent."Code" = 'WAD'
  AND NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = x."Code");

-- Hak default untuk group ADMIN: semua permission pada seluruh menu WAD.
INSERT INTO "UtlGroupMenu"
    ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT g."Id", m."Id", true, true, true, true, true, true, true
FROM "UtlGroup" g
CROSS JOIN "UtlMenu" m
WHERE g."Code" = 'ADMIN'
  AND (m."Code" = 'WAD' OR m."Code" LIKE 'WAD\_%')
  AND NOT EXISTS (
      SELECT 1 FROM "UtlGroupMenu" gm
      WHERE gm."GroupId" = g."Id" AND gm."MenuId" = m."Id"
  );

-- Peta WAD kini memuat logger, pengamatan manual, dan data lapangan;
-- selaraskan nama menu lama ("Peta Sensor") pada database yang sudah terpasang.
UPDATE "UtlMenu"
SET "Name" = 'Peta Pemantauan WAD',
    "Description" = 'Peta titik logger IoT, pengamatan manual, dan data lapangan berkoordinat.'
WHERE "Code" = 'WAD_ECS_PETA'
  AND ("Name" IS DISTINCT FROM 'Peta Pemantauan WAD'
       OR "Description" IS DISTINCT FROM 'Peta titik logger IoT, pengamatan manual, dan data lapangan berkoordinat.');

COMMIT;

-- Verifikasi struktur:
--   SELECT m."Code", m."Name", m."Route", m."MenuType", m."SortOrder", m."Active"
--   FROM "UtlMenu" m LEFT JOIN "UtlMenu" p ON p."Id" = m."ParentId"
--   WHERE m."Code" = 'WAD' OR m."Code" LIKE 'WAD\_%' OR p."Code" = 'WAD'
--   ORDER BY COALESCE(m."SortOrder", 0), m."Code";
--
-- Verifikasi duplikat route (hasil harus kosong):
--   SELECT "Route", COUNT(*) FROM "UtlMenu"
--   WHERE "Active" AND "Route" IN ('/ecs', '/ecs/stasiun-monitoring', '/ecs/monitoring-perangkat',
--     '/ecs/device-management', '/ecs/monitor-curahhujan', '/ecs/data-historis',
--     '/ecs/kalender-curah-hujan', '/ecs/peta', '/ecs/curahhujan', '/ecs/tmak', '/ecs/kelembapan')
--   GROUP BY "Route" HAVING COUNT(*) > 1;
