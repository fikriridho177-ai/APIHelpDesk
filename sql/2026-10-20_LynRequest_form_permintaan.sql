-- Form pengajuan layanan mengikuti form permintaan GIS:
--   data pemohon (nama, jabatan, departemen, perusahaan, HP/WA, email), jenis "Yang lain",
--   dan skala prioritas Normal / Sedang / Tinggi / Urgent.
-- Jalankan sekali di database HelpDesk (PostgreSQL), lalu restart API. Aman dijalankan ulang.

ALTER TABLE "LynRequest" ADD COLUMN IF NOT EXISTS "RequesterName"       varchar(150);
ALTER TABLE "LynRequest" ADD COLUMN IF NOT EXISTS "RequesterPosition"   varchar(100);
ALTER TABLE "LynRequest" ADD COLUMN IF NOT EXISTS "RequesterDepartment" varchar(150);
ALTER TABLE "LynRequest" ADD COLUMN IF NOT EXISTS "RequesterCompanies"  varchar(300);
ALTER TABLE "LynRequest" ADD COLUMN IF NOT EXISTS "RequesterPhone"      varchar(50);
ALTER TABLE "LynRequest" ADD COLUMN IF NOT EXISTS "RequesterEmail"      varchar(150);
ALTER TABLE "LynRequest" ADD COLUMN IF NOT EXISTS "OtherType"           varchar(150);

-- Prioritas lama -> baru (Rendah = Normal, Mendesak = Urgent; Normal & Tinggi tetap).
UPDATE "LynRequest" SET "Priority" = 'NORMAL' WHERE "Priority" = 'RENDAH';
UPDATE "LynRequest" SET "Priority" = 'URGENT' WHERE "Priority" = 'MENDESAK';

-- Pengajuan lama: nama pemohon diambil dari akun.
UPDATE "LynRequest" r
SET "RequesterName"  = COALESCE(r."RequesterName", u."Name"),
    "RequesterEmail" = COALESCE(r."RequesterEmail", u."Email"),
    "RequesterPhone" = COALESCE(r."RequesterPhone", u."Phone")
FROM "UtlUser" u
WHERE u."Id" = r."RequesterUserId";
