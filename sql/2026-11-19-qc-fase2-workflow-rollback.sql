-- QC Module — Rollback Fase 2 (workflow inspeksi & approval master-driven)
-- Target: helpdeskasd. Jalankan MANUAL. Mengembalikan objek yang dibuat/diubah
-- 2026-11-19-qc-fase2-workflow.sql ke kondisi Fase 1.
--
-- BATASAN (baca sebelum menjalankan):
-- * Kolom header Fase 2 (PemborongId, MandorId, InspectorUserId,
--   InspectorNameSnapshot, NoIcarLama, Revision) DI-DROP beserta isinya.
--   Kondisi aman: TrnQcInspection kosong / data Fase 2 belum dipakai.
-- * Baris TrnQcApproval revisi > 0 membuat index unik lama tidak bisa dipasang;
--   script RAISE EXCEPTION (rollback batal, data tidak hilang).
-- * Status legacy REJECTED yang dipetakan ke CLOSED tidak bisa dibedakan lagi;
--   CLOSED tetap CLOSED. Finding CLOSED lama yang menjadi VERIFIED tetap VERIFIED
--   (status VERIFIED valid di Fase 1, tinggal selangkah dari CLOSED).
-- * Nomor inspeksi format baru (> 50 karakter) membatalkan rollback via EXCEPTION.

BEGIN;

-- View baru Fase 2.
DROP VIEW IF EXISTS public."VwQcApprovalPending";
DROP VIEW IF EXISTS public."VwQcInspectionList";
-- View monitoring dibuat ulang di akhir (setelah tipe InspectionNo kembali varchar(50)).
DROP VIEW IF EXISTS public."VwQcInspectionMonitoring";
DROP VIEW IF EXISTS public."VwQcFindingMonitoring";

-- Lepas constraint & index Fase 2 (harus sebelum pemetaan balik status).
ALTER TABLE "TrnQcInspection" DROP CONSTRAINT IF EXISTS "CK_TrnQcInspection_Status";
ALTER TABLE "TrnQcInspection" DROP CONSTRAINT IF EXISTS "CK_TrnQcInspection_Revision";
ALTER TABLE "TrnQcInspection" DROP CONSTRAINT IF EXISTS "FK_TrnQcInspection_MstQcPemborong";
ALTER TABLE "TrnQcInspection" DROP CONSTRAINT IF EXISTS "FK_TrnQcInspection_MstQcMandor";
ALTER TABLE "TrnQcInspection" DROP CONSTRAINT IF EXISTS "FK_TrnQcInspection_InspectorUtlUser";
DROP INDEX IF EXISTS "IX_TrnQcInspection_PemborongId";
DROP INDEX IF EXISTS "IX_TrnQcInspection_MandorId";
DROP INDEX IF EXISTS "IX_TrnQcInspection_InspectorUserId";

ALTER TABLE "TrnQcApproval" DROP CONSTRAINT IF EXISTS "CK_TrnQcApproval_Revision";
ALTER TABLE "TrnQcApproval" DROP CONSTRAINT IF EXISTS "CK_TrnQcApproval_Status";
ALTER TABLE "TrnQcApproval" DROP CONSTRAINT IF EXISTS "CK_TrnQcApproval_Action";
ALTER TABLE "TrnQcFinding" DROP CONSTRAINT IF EXISTS "CK_TrnQcFinding_Status";

-- Pemetaan balik status inspeksi & finding (setelah CHECK dilepas).
UPDATE "TrnQcInspection" SET "Status" = 'WAITING_APPROVAL' WHERE "Status" IN ('SUBMITTED','IN_REVIEW');
UPDATE "TrnQcInspection" SET "Status" = 'REVISION_REQUIRED' WHERE "Status" = 'REVISION';
UPDATE "TrnQcFinding" SET "Status" = 'FOLLOW_UP' WHERE "Status" = 'IN_PROGRESS';
UPDATE "TrnQcFinding" SET "Status" = 'WAITING_VERIFICATION' WHERE "Status" = 'DONE';

-- Index unik lama hanya bisa dipasang bila tidak ada baris revisi ganda.
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM "TrnQcApproval"
        GROUP BY "InspectionId", "Level" HAVING count(*) > 1
    ) THEN
        RAISE EXCEPTION 'Rollback Fase 2 batal: ada baris approval revisi ganda (InspectionId, Level). Hapus/arsipkan revisi tambahan dulu.';
    END IF;
    IF EXISTS (SELECT 1 FROM "TrnQcInspection" WHERE length("InspectionNo") > 50) THEN
        RAISE EXCEPTION 'Rollback Fase 2 batal: ada InspectionNo format baru lebih dari 50 karakter.';
    END IF;
END $$;

DROP INDEX IF EXISTS "IX_TrnQcApproval_InspectionId_Level_Revision";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TrnQcApproval_InspectionId_Level"
    ON "TrnQcApproval" ("InspectionId", "Level");

ALTER TABLE "TrnQcApproval" DROP COLUMN IF EXISTS "Revision";

ALTER TABLE "TrnQcInspection" DROP COLUMN IF EXISTS "PemborongId";
ALTER TABLE "TrnQcInspection" DROP COLUMN IF EXISTS "MandorId";
ALTER TABLE "TrnQcInspection" DROP COLUMN IF EXISTS "InspectorUserId";
ALTER TABLE "TrnQcInspection" DROP COLUMN IF EXISTS "InspectorNameSnapshot";
ALTER TABLE "TrnQcInspection" DROP COLUMN IF EXISTS "NoIcarLama";
ALTER TABLE "TrnQcInspection" DROP COLUMN IF EXISTS "Revision";
ALTER TABLE "TrnQcInspection" ALTER COLUMN "InspectionNo" TYPE varchar(50);

-- Kembalikan view monitoring ke definisi Fase 1 (filter finding <> 'CLOSED').
CREATE OR REPLACE VIEW public."VwQcInspectionMonitoring" AS
SELECT
  i."Id", i."InspectionNo", i."FormId", f."Code" AS "FormCode", f."Name" AS "FormName",
  i."InspectionDate", i."CompanyId", i."CompanyNameSnapshot", i."WilayahKode",
  i."PersilSumber", i."PersilId", i."PersilKode", i."KodeAfdeling", i."Commodity",
  i."PeriodMonth", i."PeriodYear", i."Status", i."CurrentApprovalLevel",
  coalesce(x."OpenFindingCount", 0) AS "OpenFindingCount",
  coalesce(x."OverdueFindingCount", 0) AS "OverdueFindingCount",
  i."CreatedBy", i."CreatedAt", i."SubmittedAt", i."ApprovedAt", i."ClosedAt"
FROM public."TrnQcInspection" i
JOIN public."MstQcForm" f ON f."Id" = i."FormId"
LEFT JOIN (
  SELECT
    t."InspectionId",
    count(*) FILTER (WHERE t."Status" <> 'CLOSED') AS "OpenFindingCount",
    count(*) FILTER (
      WHERE t."Status" <> 'CLOSED'
        AND t."TargetDate" IS NOT NULL
        AND t."TargetDate" < (current_timestamp AT TIME ZONE 'Asia/Jakarta')::date
    ) AS "OverdueFindingCount"
  FROM public."TrnQcFinding" t
  GROUP BY t."InspectionId"
) x ON x."InspectionId" = i."Id";

CREATE OR REPLACE VIEW public."VwQcFindingMonitoring" AS
SELECT
  f."Id", f."FindingNo", f."InspectionId", i."InspectionNo", i."InspectionDate",
  i."WilayahKode", i."PersilKode", f."Category", f."Description", f."Severity",
  f."PicUserId", coalesce(f."PicNameSnapshot", u."Name") AS "PicName",
  f."TargetDate", f."Status",
  (f."Status" <> 'CLOSED'
    AND f."TargetDate" IS NOT NULL
    AND f."TargetDate" < (current_timestamp AT TIME ZONE 'Asia/Jakarta')::date) AS "IsOverdue",
  f."FollowUpNote", f."VerifiedByUserId", f."VerifiedDate", f."CreatedAt", f."UpdatedAt"
FROM public."TrnQcFinding" f
JOIN public."TrnQcInspection" i ON i."Id" = f."InspectionId"
LEFT JOIN public."UtlUser" u ON u."Id" = f."PicUserId";

ALTER TABLE "MstQcFormApprovalStep" DROP COLUMN IF EXISTS "IsCreatorStep";

COMMIT;