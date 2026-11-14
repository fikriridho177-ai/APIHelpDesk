-- QC Module — Menu seed (UtlMenu) Phase 1
-- Target database : helpdeskasd (PostgreSQL). Jalankan SETELAH 2026-11-11-qc-core-schema.sql.
-- Idempotent. Struktur:
--   QC (GROUP)
--     ├── QC_DASHBOARD       /qc/dashboard
--     ├── QC_INSPEKSI        /qc/inspeksi
--     ├── QC_BUAT_INSPEKSI   /qc/buat-inspeksi
--     ├── QC_APPROVAL        /qc/approval
--     ├── QC_FINDING         /qc/finding
--     ├── QC_MONITORING      /qc/monitoring
--     └── QC_REPORT          /qc/report
-- Hak default: group ADMIN mendapat semua permission QC (langsung pakai).
-- Group lain diatur lewat Hak Akses > Akses Menu (UI).

BEGIN;

-- Root grup menu QC.
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT NULL, 'QC', 'QC', '/qc', 'clipboard-check', 'Modul Quality Control: inspeksi, approval, temuan, monitoring.',
       'GROUP', 100, true, 'system', now()
WHERE NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = 'QC');

-- Anak menu (parent = root QC).
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT parent."Id", x."Code", x."Name", x."Route", COALESCE(x."Icon", 'circle'), x."Description", 'MENU', x."SortOrder", true, 'system', now()
FROM "UtlMenu" parent
CROSS JOIN (VALUES
    ('QC_DASHBOARD',     'Dashboard',          '/qc/dashboard',       'gauge',        'Ringkasan status inspeksi & temuan QC.',                 1),
    ('QC_INSPEKSI',       'Semua Inspeksi',     '/qc/inspeksi',        'list',         'Daftar seluruh inspeksi QC.',                            2),
    ('QC_BUAT_INSPEKSI',  'Buat Inspeksi',      '/qc/buat-inspeksi',   'plus-circle',  'Buat inspeksi QC baru (pilih form).',                    3),
    ('QC_APPROVAL',       'Approval',           '/qc/approval',        'check-circle', 'Antrian approval inspeksi QC.',                          4),
    ('QC_FINDING',        'Finding / Temuan',   '/qc/finding',         'alert-triangle','Daftar temuan & tindak lanjut QC.',                      5),
    ('QC_MONITORING',     'Monitoring',         '/qc/monitoring',      'activity',     'Monitoring inspeksi QC (segera hadir).',                  6),
    ('QC_REPORT',         'Report / Export',    '/qc/report',          'file-text',    'Laporan & export Excel QC (segera hadir).',               7)
) AS x("Code", "Name", "Route", "Icon", "Description", "SortOrder")
WHERE parent."Code" = 'QC'
  AND NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = x."Code");

-- Hak default untuk group ADMIN: semua permission pada seluruh menu QC.
INSERT INTO "UtlGroupMenu"
    ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT g."Id", m."Id", true, true, true, true, true, true, true
FROM "UtlGroup" g
CROSS JOIN "UtlMenu" m
WHERE g."Code" = 'ADMIN'
  AND (m."Code" = 'QC' OR m."Code" LIKE 'QC\_%')
  AND NOT EXISTS (
      SELECT 1 FROM "UtlGroupMenu" gm
      WHERE gm."GroupId" = g."Id" AND gm."MenuId" = m."Id"
  );

COMMIT;

-- Verifikasi:
--   SELECT m."Code", m."Name", m."Route", m."MenuType", m."SortOrder"
--   FROM "UtlMenu" m LEFT JOIN "UtlMenu" p ON p."Id" = m."ParentId"
--   WHERE m."Code" = 'QC' OR m."Code" LIKE 'QC\_%' OR p."Code" = 'QC'
--   ORDER BY COALESCE(m."SortOrder", 0), m."Code";
