-- Portal: widget halaman utama sebagai menu bertipe WIDGET di bawah menu induk PORTAL.
-- Siapa melihat widget apa diatur di Hak Akses > Akses Menu (kolom View).
-- Jalankan sekali di database HelpDesk (PostgreSQL). Aman dijalankan ulang.

-- 1. Menu induk PORTAL
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT NULL, 'PORTAL', 'Portal', NULL, 'grid', 'GROUP', 900, true, 'system', now()
WHERE NOT EXISTS (SELECT 1 FROM "UtlMenu" WHERE "Code" = 'PORTAL');

-- 2. Enam widget versi pertama (urutan tampil = SortOrder)
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT p."Id", v.code, v.name, NULL, v.icon, v.description, 'WIDGET', v.sort_order, true, 'system', now()
FROM (SELECT "Id" FROM "UtlMenu" WHERE "Code" = 'PORTAL') AS p
CROSS JOIN (VALUES
    ('WGT_KATALOG',         'Katalog Layanan', 'grid',    10, 'Kartu divisi layanan untuk membuat pengajuan.'),
    ('WGT_ANTRIAN',         'Antrian Tiket',   'inbox',   20, 'Tiket yang menunggu tindakan petugas.'),
    ('WGT_TIKET_SAYA',      'Tiket Saya',      'ticket',  30, '5 pengajuan terakhir milik user.'),
    ('WGT_AKSES_CEPAT',     'Akses Cepat',     'desktop', 40, 'Pintasan ke aplikasi lain yang boleh diakses.'),
    ('WGT_RINGKASAN_TIKET', 'Ringkasan Tiket', 'chart',   50, 'Jumlah tiket per status.'),
    ('WGT_KPI_DIVISI',      'KPI Divisi',      'chart',   60, 'Tiket per divisi & rata-rata waktu selesai (30 hari).')
) AS v(code, name, icon, sort_order, description)
WHERE NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = v.code);

-- 3. Hak View default: Katalog, Tiket Saya, Akses Cepat untuk semua group aktif.
--    Hanya menambah baris yang belum ada; pengaturan admin yang sudah ada tidak diubah.
INSERT INTO "UtlGroupMenu" ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT g."Id", m."Id", true, false, false, false, false, false, false
FROM "UtlGroup" g
CROSS JOIN "UtlMenu" m
WHERE g."Active" = true
  AND m."Code" IN ('WGT_KATALOG', 'WGT_TIKET_SAYA', 'WGT_AKSES_CEPAT')
  AND NOT EXISTS (
      SELECT 1 FROM "UtlGroupMenu" gm WHERE gm."GroupId" = g."Id" AND gm."MenuId" = m."Id"
  );

-- Widget Antrian, Ringkasan Tiket dan KPI Divisi diberikan admin lewat Akses Menu.
-- Agar KPI manajemen mencakup semua divisi: beri group manajemen hak Read di setiap
-- SUB-LAYANAN (menu tipe MENU di bawah LYN_RNA, LYN_GIS, LYN_WAD, LYN_QC, LYN_NAT) dan centang
-- "Semua Departemen". Hak di menu divisinya saja (GROUP) tidak cukup.
