-- WAD Module — RBAC per jabatan + penyeragaman struktur menu/route.
-- Target database : helpdeskasd (PostgreSQL). Idempotent, aman dijalankan ulang.
--
-- Isi:
--   1. Semua menu WAD_ECS_* dipindahkan ke bawah satu root grup WAD.
--   2. Menu pengamatan lapangan (Pengamatan Air, Subsiden Gambut, Lahan
--      Tergenang, Projek WAD) didaftarkan sebagai menu + permission resmi.
--   3. Nama & route disamakan dengan kartu Pusat Layanan WAD dan route frontend:
--        /ecs, /ecs/monitoring, /ecs/map, /ecs/rain-calendar,
--        /ecs/pengamatan-air, /ecs/subsiden, /ecs/lahan-tergenang,
--        /ecs/projek, /ecs/device-status, /ecs/device-management
--      Menu parameter legacy tetap aktif untuk hak akses, tetapi tidak lagi
--      ditampilkan sebagai item navbar (diatur di frontend).
--   4. Grup jabatan WAD: MANAGER_WAD, ADMIN_WAD, STAFF_WAD (petugas lapangan /
--      field), IOT_ENGINEER. Hak ADMIN & SuperAdmin tidak diubah.
--
-- Tingkat hak per menu:
--   R = View + Read
--   W = View + Read + Create
--   M = View + Read + Create + Update + Approve + Export (tanpa Delete)
--   F = seluruh tujuh hak (View/Read/Create/Update/Delete/Approve/Export)

BEGIN;

/* =========================================================
   1. Root grup WAD
========================================================= */
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT NULL, 'WAD', 'WAD', NULL, 'droplet',
       'Modul Water & Device: monitoring air, perangkat IoT, dan pengamatan lapangan.',
       'GROUP', 110, true, 'system', now()
WHERE NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = 'WAD');

UPDATE "UtlMenu"
SET "Active" = true, "UpdatedBy" = 'system', "UpdatedAt" = now()
WHERE "Code" = 'WAD' AND NOT "Active";

/* =========================================================
   2. Menu anak WAD (lengkap, termasuk menu lapangan)
========================================================= */
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT parent."Id", x."Code", x."Name", x."Route", x."Icon", x."Description", 'MENU', x."SortOrder", true, 'system', now()
FROM "UtlMenu" parent
CROSS JOIN (VALUES
    ('WAD_ECS_DASHBOARD',         'Pusat Layanan WAD',            '/ecs',                        'droplet',        'Hub layanan WAD: monitoring air, perangkat IoT, dan pengamatan lapangan.', 1),
    ('WAD_ECS_STASIUN',           'Monitoring Air & Curah Hujan', '/ecs/monitoring',             'activity',       'Pantau sensor Curah Hujan, TMAK, dan TMAT.',                              2),
    ('WAD_ECS_PETA',              'Peta Pemantauan WAD',          '/ecs/map',                    'map',            'Peta titik logger IoT, pengamatan manual, dan data lapangan berkoordinat.', 3),
    ('WAD_ECS_KALENDER_CH',       'Kalender Pengamatan',          '/ecs/rain-calendar',          'calendar',       'Telusuri pembacaan harian berdasarkan parameter dan titik.',              4),
    ('WAD_ECS_PENGAMATAN_AIR',    'Pengamatan Air',               '/ecs/pengamatan-air',         'gate',           'Buka-tutup pintu air, overflow, dan bangunan air.',                       5),
    ('WAD_ECS_SUBSIDEN_GAMBUT',   'Subsiden Gambut',              '/ecs/subsiden',               'subsidence',     'Patok dan perubahan permukaan tanah gambut.',                             6),
    ('WAD_ECS_LAHAN_TERGENANG',   'Lahan Tergenang',              '/ecs/lahan-tergenang',        'flood',          'Pengamatan genangan pada persil dan blok.',                               7),
    ('WAD_ECS_PROJEK',            'Projek WAD',                   '/ecs/projek',                 'clipboard',      'PIC, jadwal, dan progres pekerjaan WAD.',                                 8),
    ('WAD_ECS_PERANGKAT',         'Status Perangkat IoT',         '/ecs/device-status',          'desktop',        'Koneksi dan waktu pengiriman terakhir perangkat.',                        9),
    ('WAD_ECS_DEVICE_MANAGEMENT', 'Manajemen Perangkat & OTA',    '/ecs/device-management',      'settings',       'Kelola sensor, konfigurasi, dan firmware perangkat.',                    10),
    ('WAD_ECS_MONITOR_CH',        'Monitor Curah Hujan',          '/ecs/monitor-curahhujan',     'cloud-rain',     'Monitor curah hujan (sub-halaman monitoring).',                          11),
    ('WAD_ECS_DATA_HISTORIS',     'Data Historis',                NULL,                          'database',       'Riwayat pembacaan sensor dari detail sensor.',                            12),
    ('WAD_ECS_CURAH_HUJAN',       'Curah Hujan',                  '/ecs/curahhujan',             'cloud-rain',     'Data curah hujan (sub-halaman monitoring).',                             13),
    ('WAD_ECS_TMAK',              'Tinggi Muka Air Kanal (TMAK)', '/ecs/tmak',                   'waves',          'Tinggi muka air kanal (sub-halaman monitoring).',                        14),
    ('WAD_ECS_KELEMBAPAN',        'Kelembaban Tanah',             '/ecs/kelembapan',             'sprout',         'Kelembaban tanah (sub-halaman monitoring).',                             15)
) AS x("Code", "Name", "Route", "Icon", "Description", "SortOrder")
WHERE parent."Code" = 'WAD'
  AND NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = x."Code");

/* =========================================================
   3. Rapikan parent, nama, route, dan urutan menu WAD
   Route hanya diisi bila belum dipegang menu aktif lain.
========================================================= */
UPDATE "UtlMenu" m
SET "ParentId" = root."Id",
    "Name" = x."Name",
    "Route" = CASE
        WHEN x."Route" IS NULL THEN NULL
        WHEN NOT EXISTS (
            SELECT 1 FROM "UtlMenu" o
            WHERE o."Route" = x."Route" AND o."Active" AND o."Code" <> m."Code"
        ) THEN x."Route"
        ELSE m."Route"
    END,
    "Icon" = x."Icon",
    "Description" = x."Description",
    "MenuType" = 'MENU',
    "SortOrder" = x."SortOrder",
    "Active" = true,
    "UpdatedBy" = 'system',
    "UpdatedAt" = now()
FROM (VALUES
    ('WAD_ECS_DASHBOARD',         'Pusat Layanan WAD',            '/ecs',                        'droplet',        'Hub layanan WAD: monitoring air, perangkat IoT, dan pengamatan lapangan.', 1),
    ('WAD_ECS_STASIUN',           'Monitoring Air & Curah Hujan', '/ecs/monitoring',             'activity',       'Pantau sensor Curah Hujan, TMAK, dan TMAT.',                              2),
    ('WAD_ECS_PETA',              'Peta Pemantauan WAD',          '/ecs/map',                    'map',            'Peta titik logger IoT, pengamatan manual, dan data lapangan berkoordinat.', 3),
    ('WAD_ECS_KALENDER_CH',       'Kalender Pengamatan',          '/ecs/rain-calendar',          'calendar',       'Telusuri pembacaan harian berdasarkan parameter dan titik.',              4),
    ('WAD_ECS_PENGAMATAN_AIR',    'Pengamatan Air',               '/ecs/pengamatan-air',         'gate',           'Buka-tutup pintu air, overflow, dan bangunan air.',                       5),
    ('WAD_ECS_SUBSIDEN_GAMBUT',   'Subsiden Gambut',              '/ecs/subsiden',               'subsidence',     'Patok dan perubahan permukaan tanah gambut.',                             6),
    ('WAD_ECS_LAHAN_TERGENANG',   'Lahan Tergenang',              '/ecs/lahan-tergenang',        'flood',          'Pengamatan genangan pada persil dan blok.',                               7),
    ('WAD_ECS_PROJEK',            'Projek WAD',                   '/ecs/projek',                 'clipboard',      'PIC, jadwal, dan progres pekerjaan WAD.',                                 8),
    ('WAD_ECS_PERANGKAT',         'Status Perangkat IoT',         '/ecs/device-status',          'desktop',        'Koneksi dan waktu pengiriman terakhir perangkat.',                        9),
    ('WAD_ECS_DEVICE_MANAGEMENT', 'Manajemen Perangkat & OTA',    '/ecs/device-management',      'settings',       'Kelola sensor, konfigurasi, dan firmware perangkat.',                    10),
    ('WAD_ECS_MONITOR_CH',        'Monitor Curah Hujan',          '/ecs/monitor-curahhujan',     'cloud-rain',     'Monitor curah hujan (sub-halaman monitoring).',                          11),
    ('WAD_ECS_DATA_HISTORIS',     'Data Historis',                NULL,                          'database',       'Riwayat pembacaan sensor dari detail sensor.',                            12),
    ('WAD_ECS_CURAH_HUJAN',       'Curah Hujan',                  '/ecs/curahhujan',             'cloud-rain',     'Data curah hujan (sub-halaman monitoring).',                             13),
    ('WAD_ECS_TMAK',              'Tinggi Muka Air Kanal (TMAK)', '/ecs/tmak',                   'waves',          'Tinggi muka air kanal (sub-halaman monitoring).',                        14),
    ('WAD_ECS_KELEMBAPAN',        'Kelembaban Tanah',             '/ecs/kelembapan',             'sprout',         'Kelembaban tanah (sub-halaman monitoring).',                             15)
) AS x("Code", "Name", "Route", "Icon", "Description", "SortOrder")
JOIN "UtlMenu" root ON root."Code" = 'WAD'
WHERE m."Code" = x."Code";

/* =========================================================
   4. Grup jabatan WAD (tidak menyentuh grup lama)
========================================================= */
INSERT INTO "UtlGroup"
    ("Code", "Name", "Description", "IsSuperAdmin", "AllCompanies", "AllDepartments", "AllItemSubCategories", "Active", "CreatedBy", "CreatedAt")
SELECT x."Code", x."Name", x."Description", false, false, false, false, true, 'system', now()
FROM (VALUES
    ('MANAGER_WAD',  'Manager WAD',          'Akses penuh seluruh modul WAD, termasuk persetujuan pengamatan lapangan.'),
    ('ADMIN_WAD',    'Admin WAD',            'Input dan verifikasi pengamatan harian/mingguan; monitoring tanpa pengaturan perangkat.'),
    ('STAFF_WAD',    'Staff Lapangan WAD',   'Petugas lapangan (field): baca monitoring/peta/kalender dan input pengamatan lapangan.'),
    ('IOT_ENGINEER', 'IoT Engineer',         'Mengelola perangkat IoT, sensor, pin, dan firmware/OTA.')
) AS x("Code", "Name", "Description")
WHERE NOT EXISTS (SELECT 1 FROM "UtlGroup" g WHERE g."Code" = x."Code");

/* =========================================================
   5. Hak akses per jabatan
========================================================= */

-- Manager WAD: penuh pada seluruh menu WAD (root + anak).
INSERT INTO "UtlGroupMenu"
    ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT g."Id", m."Id", true, true, true, true, true, true, true
FROM "UtlGroup" g
CROSS JOIN "UtlMenu" m
WHERE g."Code" = 'MANAGER_WAD'
  AND (m."Code" = 'WAD' OR m."Code" LIKE 'WAD!_%' ESCAPE '!')
ON CONFLICT ("GroupId", "MenuId") DO NOTHING;

-- Admin WAD, Staff lapangan, dan IoT Engineer.
INSERT INTO "UtlGroupMenu"
    ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT g."Id", m."Id",
       true,
       true,
       x."Level" IN ('W', 'M', 'F'),
       x."Level" IN ('M', 'F'),
       x."Level" = 'F',
       x."Level" IN ('M', 'F'),
       x."Level" IN ('M', 'F')
FROM (VALUES
    -- Admin WAD: monitoring & peta read-only; input + verifikasi modul lapangan.
    ('ADMIN_WAD', 'WAD',                      'R'),
    ('ADMIN_WAD', 'WAD_ECS_DASHBOARD',        'R'),
    ('ADMIN_WAD', 'WAD_ECS_STASIUN',          'R'),
    ('ADMIN_WAD', 'WAD_ECS_MONITOR_CH',       'R'),
    ('ADMIN_WAD', 'WAD_ECS_DATA_HISTORIS',    'R'),
    ('ADMIN_WAD', 'WAD_ECS_KALENDER_CH',      'R'),
    ('ADMIN_WAD', 'WAD_ECS_PETA',             'R'),
    ('ADMIN_WAD', 'WAD_ECS_PERANGKAT',        'R'),
    ('ADMIN_WAD', 'WAD_ECS_CURAH_HUJAN',      'R'),
    ('ADMIN_WAD', 'WAD_ECS_TMAK',             'R'),
    ('ADMIN_WAD', 'WAD_ECS_KELEMBAPAN',       'R'),
    ('ADMIN_WAD', 'WAD_ECS_PENGAMATAN_AIR',   'M'),
    ('ADMIN_WAD', 'WAD_ECS_SUBSIDEN_GAMBUT',  'M'),
    ('ADMIN_WAD', 'WAD_ECS_LAHAN_TERGENANG',  'M'),
    ('ADMIN_WAD', 'WAD_ECS_PROJEK',           'M'),

    -- Staff lapangan: baca monitoring/peta/kalender, input pengamatan lapangan.
    ('STAFF_WAD', 'WAD',                      'R'),
    ('STAFF_WAD', 'WAD_ECS_DASHBOARD',        'R'),
    ('STAFF_WAD', 'WAD_ECS_STASIUN',          'R'),
    ('STAFF_WAD', 'WAD_ECS_MONITOR_CH',       'R'),
    ('STAFF_WAD', 'WAD_ECS_DATA_HISTORIS',    'R'),
    ('STAFF_WAD', 'WAD_ECS_KALENDER_CH',      'R'),
    ('STAFF_WAD', 'WAD_ECS_PETA',             'R'),
    ('STAFF_WAD', 'WAD_ECS_PERANGKAT',        'R'),
    ('STAFF_WAD', 'WAD_ECS_CURAH_HUJAN',      'R'),
    ('STAFF_WAD', 'WAD_ECS_TMAK',             'R'),
    ('STAFF_WAD', 'WAD_ECS_KELEMBAPAN',       'R'),
    ('STAFF_WAD', 'WAD_ECS_PENGAMATAN_AIR',   'W'),
    ('STAFF_WAD', 'WAD_ECS_SUBSIDEN_GAMBUT',  'W'),
    ('STAFF_WAD', 'WAD_ECS_LAHAN_TERGENANG',  'W'),

    -- IoT Engineer: penuh pada manajemen perangkat & OTA, baca monitoring.
    ('IOT_ENGINEER', 'WAD',                      'R'),
    ('IOT_ENGINEER', 'WAD_ECS_DASHBOARD',        'R'),
    ('IOT_ENGINEER', 'WAD_ECS_STASIUN',          'R'),
    ('IOT_ENGINEER', 'WAD_ECS_MONITOR_CH',       'R'),
    ('IOT_ENGINEER', 'WAD_ECS_DATA_HISTORIS',    'R'),
    ('IOT_ENGINEER', 'WAD_ECS_KALENDER_CH',      'R'),
    ('IOT_ENGINEER', 'WAD_ECS_PETA',             'R'),
    ('IOT_ENGINEER', 'WAD_ECS_PERANGKAT',        'R'),
    ('IOT_ENGINEER', 'WAD_ECS_CURAH_HUJAN',      'R'),
    ('IOT_ENGINEER', 'WAD_ECS_TMAK',             'R'),
    ('IOT_ENGINEER', 'WAD_ECS_KELEMBAPAN',       'R'),
    ('IOT_ENGINEER', 'WAD_ECS_DEVICE_MANAGEMENT', 'F')
) AS x("GroupCode", "MenuCode", "Level")
JOIN "UtlGroup" g ON g."Code" = x."GroupCode"
JOIN "UtlMenu" m ON m."Code" = x."MenuCode"
ON CONFLICT ("GroupId", "MenuId") DO NOTHING;

-- Grup ADMIN dan SuperAdmin tetap memegang seluruh menu WAD.
INSERT INTO "UtlGroupMenu"
    ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT g."Id", m."Id", true, true, true, true, true, true, true
FROM "UtlGroup" g
CROSS JOIN "UtlMenu" m
WHERE g."Code" = 'ADMIN'
  AND (m."Code" = 'WAD' OR m."Code" LIKE 'WAD!_%' ESCAPE '!')
ON CONFLICT ("GroupId", "MenuId") DO NOTHING;

COMMIT;

-- Verifikasi struktur & urutan menu WAD:
--   SELECT m."Code", m."Name", m."Route", m."SortOrder", p."Code" AS parent
--   FROM "UtlMenu" m LEFT JOIN "UtlMenu" p ON p."Id" = m."ParentId"
--   WHERE m."Code" = 'WAD' OR m."Code" LIKE 'WAD!_%' ESCAPE '!'
--   ORDER BY m."SortOrder", m."Code";
--
-- Verifikasi grup & jumlah hak:
--   SELECT g."Code", g."Name", count(gm.*) AS menus
--   FROM "UtlGroup" g LEFT JOIN "UtlGroupMenu" gm ON gm."GroupId" = g."Id"
--   GROUP BY g."Code", g."Name" ORDER BY g."Code";
--
-- Verifikasi duplikat route (hasil harus kosong):
--   SELECT "Route", count(*) FROM "UtlMenu"
--   WHERE "Active" AND "Route" LIKE '/ecs%'
--   GROUP BY "Route" HAVING count(*) > 1;
