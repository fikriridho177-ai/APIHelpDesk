-- 2026-10-22  Menu Peta GIS (viewer WebGIS_PT_RSUP di dalam HelpDesk)
-- Jalankan di database helpdeskasd SEBELUM restart API. Aman dijalankan ulang.
-- Database WebGIS sendiri TIDAK diubah: API hanya membacanya (read-only) lewat ConnectionStrings:WebGis.

BEGIN;

-- Menu Peta GIS di bawah grup Monitoring (bisa dipindah / di-rename di Manajemen Menu;
-- halaman dikenali dari kode GIS_PETA, bukan dari route).
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT p."Id", 'GIS_PETA', 'Peta GIS', '/gis/peta', 'map',
       'Peta perkebunan dari WebGIS: layer, legenda, ukur jarak & luas.', 'MENU', 90, true, 'system', now()
FROM "UtlMenu" p
WHERE p."Code" = 'MONITORING'
  AND NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = 'GIS_PETA');

-- Hak default: View + Read untuk group petugas (Read / Update / Approve) di divisi GIS atau RNA.
-- Hanya menambah baris yang belum ada. Group lain bisa diberi lewat Hak Akses > Akses Menu.
INSERT INTO "UtlGroupMenu" ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT DISTINCT gm."GroupId", peta."Id", true, true, false, false, false, false, false
FROM "UtlGroupMenu" gm
JOIN "UtlMenu" d ON d."Id" = gm."MenuId" AND d."Code" IN ('LYN_GIS', 'LYN_RNA')
CROSS JOIN (SELECT "Id" FROM "UtlMenu" WHERE "Code" = 'GIS_PETA') AS peta
WHERE (gm."CanRead" OR gm."CanUpdate" OR gm."CanApprove")
  AND NOT EXISTS (SELECT 1 FROM "UtlGroupMenu" x WHERE x."GroupId" = gm."GroupId" AND x."MenuId" = peta."Id");

COMMIT;

-- Cek hasil:
--   SELECT m."Code", p."Code" AS parent, m."Route" FROM "UtlMenu" m LEFT JOIN "UtlMenu" p ON p."Id" = m."ParentId" WHERE m."Code" = 'GIS_PETA';
--   SELECT g."Name", gm.* FROM "UtlGroupMenu" gm JOIN "UtlGroup" g ON g."Id" = gm."GroupId"
--   WHERE gm."MenuId" = (SELECT "Id" FROM "UtlMenu" WHERE "Code" = 'GIS_PETA');
