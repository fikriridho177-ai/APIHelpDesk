-- QC Module — Rollback Fase 1 (fondasi & master)
-- Target: helpdeskasd. Jalankan MANUAL dalam transaksi bila ingin bisa batal.
-- Menghapus HANYA objek yang dibuat 2026-11-19-qc-foundation.sql.
-- Aman selama belum ada inspeksi yang merujuk form baru (kondisi Fase 1:
-- TrnQcInspection kosong). Bila sudah ada transaksi, JANGAN jalankan rollback ini.
-- Kolom metadata MstQcForm ikut di-drop (tidak memuat data transaksi).

BEGIN;

-- Menu & hak akses QC_MASTER.
DELETE FROM "UtlGroupMenu" gm
USING "UtlMenu" m
WHERE gm."MenuId" = m."Id" AND m."Code" = 'QC_MASTER';
DELETE FROM "UtlMenu" WHERE "Code" = 'QC_MASTER';

-- Tabel master baru (urutan FK: anak dulu).
DROP TABLE IF EXISTS "MstQcStandard";
DROP TABLE IF EXISTS "MstQcGrade";
DROP TABLE IF EXISTS "MstQcJenisPupuk";
DROP TABLE IF EXISTS "MstQcMandor";
DROP TABLE IF EXISTS "MstQcPemborong";
DROP TABLE IF EXISTS "MstQcPersil";
DROP TABLE IF EXISTS "MstQcNumberSeq";
DROP TABLE IF EXISTS "MstQcFormApprovalStep";

-- Form yang di-seed Fase 1 dan belum dipakai transaksi.
DELETE FROM "MstQcForm"
WHERE "Code" IN (
    'QC-PL-NNS', 'QC-HORMON-NNS', 'QC-PANEN-NNS', 'QC-PEMUPUKAN-KLP',
    'QC-PUPUK-POWER-NNS', 'QC-PUPUK-DASAR-NNS', 'QC-PENGIRIMAN-NNS', 'QC-PANEN-KLP-RSTM'
)
AND NOT EXISTS (SELECT 1 FROM "TrnQcInspection" i WHERE i."FormId" = "MstQcForm"."Id");

-- Kembalikan index unik Code tunggal bila tidak ada duplikat Code (versi > 1).
DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM "MstQcForm" GROUP BY "Code" HAVING count(*) > 1
    ) THEN
        DROP INDEX IF EXISTS "IX_MstQcForm_Code_Version";
        CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcForm_Code" ON "MstQcForm" ("Code");
    END IF;
END $$;

DROP INDEX IF EXISTS "IX_MstQcForm_ExcelCode";

ALTER TABLE "MstQcForm" DROP COLUMN IF EXISTS "Version";
ALTER TABLE "MstQcForm" DROP COLUMN IF EXISTS "EffectiveFrom";
ALTER TABLE "MstQcForm" DROP COLUMN IF EXISTS "Pattern";
ALTER TABLE "MstQcForm" DROP COLUMN IF EXISTS "TemplateFile";
ALTER TABLE "MstQcForm" DROP COLUMN IF EXISTS "PrintTitle";
ALTER TABLE "MstQcForm" DROP COLUMN IF EXISTS "ExcelCode";

COMMIT;