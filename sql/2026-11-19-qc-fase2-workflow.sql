-- QC Module — Fase 2: Workflow inspeksi, approval master-driven, penomoran atomik
-- Target database : helpdeskasd (PostgreSQL). Jalankan MANUAL setelah review.
-- Idempotent: ADD COLUMN IF NOT EXISTS, CREATE INDEX IF NOT EXISTS, constraint
--             dicek lewat pg_constraint, UPDATE pemetaan status lama aman diulang.
-- Script ini TANPA BEGIN/COMMIT internal supaya bisa dibungkus transaksi uji
-- (apply committed, re-run idempotensi, dan uji DB kosong via ROLLBACK).
-- Tidak men-drop kolom/tabel berisi data. Index unik TrnQcApproval diganti
-- (InspectionId, Level, Revision) karena siklus revisi menyimpan baris per revisi.
-- Rollback: 2026-11-19-qc-fase2-workflow-rollback.sql

-- =========================================================
-- 1. MstQcFormApprovalStep — tandai baris pembuat (auto saat submit)
--    Konvensi seed Fase 1: Level 1 "Dilaporkan" = pembuat dokumen.
-- =========================================================
ALTER TABLE "MstQcFormApprovalStep" ADD COLUMN IF NOT EXISTS "IsCreatorStep" boolean NOT NULL DEFAULT false;

UPDATE "MstQcFormApprovalStep"
SET "IsCreatorStep" = true
WHERE "Level" = 1 AND lower("Label") = 'dilaporkan' AND "IsCreatorStep" = false;

-- =========================================================
-- 2. TrnQcInspection — field header Fase 2 + siklus revisi
-- =========================================================
ALTER TABLE "TrnQcInspection" ADD COLUMN IF NOT EXISTS "PemborongId"          int          NULL;
ALTER TABLE "TrnQcInspection" ADD COLUMN IF NOT EXISTS "MandorId"             int          NULL;
ALTER TABLE "TrnQcInspection" ADD COLUMN IF NOT EXISTS "InspectorUserId"      int          NULL;
ALTER TABLE "TrnQcInspection" ADD COLUMN IF NOT EXISTS "InspectorNameSnapshot" varchar(150) NULL;
-- Nomor ICAR lama: format menunggu konfirmasi (pertanyaan §6), disimpan teks bebas.
ALTER TABLE "TrnQcInspection" ADD COLUMN IF NOT EXISTS "NoIcarLama"           varchar(100) NULL;
ALTER TABLE "TrnQcInspection" ADD COLUMN IF NOT EXISTS "Revision"             int          NOT NULL DEFAULT 0;

-- Nomor baru berformat {FormCode}/{Wilayah}/{yyyy}/{MM}/{seq} — perlebar kolom.
-- Semua view yang menyentuh kolom ini di-drop dulu (read-only, tanpa data),
-- lalu dibuat ulang di bagian 5 & 6. Wajib agar script idempotent.
DROP VIEW IF EXISTS public."VwQcInspectionList";
DROP VIEW IF EXISTS public."VwQcApprovalPending";
DROP VIEW IF EXISTS public."VwQcInspectionMonitoring";
DROP VIEW IF EXISTS public."VwQcFindingMonitoring";
ALTER TABLE "TrnQcInspection" ALTER COLUMN "InspectionNo" TYPE varchar(150);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TrnQcInspection_MstQcPemborong') THEN
        ALTER TABLE "TrnQcInspection" ADD CONSTRAINT "FK_TrnQcInspection_MstQcPemborong"
            FOREIGN KEY ("PemborongId") REFERENCES "MstQcPemborong" ("Id") ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TrnQcInspection_MstQcMandor') THEN
        ALTER TABLE "TrnQcInspection" ADD CONSTRAINT "FK_TrnQcInspection_MstQcMandor"
            FOREIGN KEY ("MandorId") REFERENCES "MstQcMandor" ("Id") ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'FK_TrnQcInspection_InspectorUtlUser') THEN
        ALTER TABLE "TrnQcInspection" ADD CONSTRAINT "FK_TrnQcInspection_InspectorUtlUser"
            FOREIGN KEY ("InspectorUserId") REFERENCES "UtlUser" ("Id") ON DELETE SET NULL;
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_TrnQcInspection_Revision') THEN
        ALTER TABLE "TrnQcInspection" ADD CONSTRAINT "CK_TrnQcInspection_Revision" CHECK ("Revision" >= 0);
    END IF;
END $$;

CREATE INDEX IF NOT EXISTS "IX_TrnQcInspection_PemborongId" ON "TrnQcInspection" ("PemborongId");
CREATE INDEX IF NOT EXISTS "IX_TrnQcInspection_MandorId" ON "TrnQcInspection" ("MandorId");
CREATE INDEX IF NOT EXISTS "IX_TrnQcInspection_InspectorUserId" ON "TrnQcInspection" ("InspectorUserId");

-- Pemetaan status lama -> baru (idempotent; nilai lama tidak tersisa setelah ini).
-- WAITING_APPROVAL level 1 = belum direview siapa pun -> SUBMITTED;
-- level > 1 sudah melewati review pertama -> IN_REVIEW.
-- REJECTED (terminal lama) -> CLOSED; ClosedAt sudah terisi saat reject.
UPDATE "TrnQcInspection" SET "Status" = 'SUBMITTED'
WHERE "Status" = 'WAITING_APPROVAL' AND "CurrentApprovalLevel" <= 1;
UPDATE "TrnQcInspection" SET "Status" = 'IN_REVIEW'
WHERE "Status" = 'WAITING_APPROVAL' AND "CurrentApprovalLevel" > 1;
UPDATE "TrnQcInspection" SET "Status" = 'REVISION'
WHERE "Status" = 'REVISION_REQUIRED';
UPDATE "TrnQcInspection" SET "Status" = 'CLOSED', "ClosedAt" = coalesce("ClosedAt", now())
WHERE "Status" = 'REJECTED';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_TrnQcInspection_Status') THEN
        ALTER TABLE "TrnQcInspection" ADD CONSTRAINT "CK_TrnQcInspection_Status"
            CHECK ("Status" IN ('DRAFT','SUBMITTED','IN_REVIEW','REVISION','APPROVED','CLOSED'));
    END IF;
END $$;

-- =========================================================
-- 3. TrnQcApproval — baris per (InspectionId, Level, Revision)
--    Baris revisi lama TETAP disimpan (audit); revisi baru = baris baru.
-- =========================================================
ALTER TABLE "TrnQcApproval" ADD COLUMN IF NOT EXISTS "Revision" int NOT NULL DEFAULT 0;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_TrnQcApproval_Revision') THEN
        ALTER TABLE "TrnQcApproval" ADD CONSTRAINT "CK_TrnQcApproval_Revision" CHECK ("Revision" >= 0);
    END IF;
    -- REJECTED / REJECT dipertahankan di CHECK hanya untuk baris legacy (tidak ditulis lagi).
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_TrnQcApproval_Status') THEN
        ALTER TABLE "TrnQcApproval" ADD CONSTRAINT "CK_TrnQcApproval_Status"
            CHECK ("Status" IN ('PENDING','APPROVED','REVISION_REQUIRED','REJECTED'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_TrnQcApproval_Action') THEN
        ALTER TABLE "TrnQcApproval" ADD CONSTRAINT "CK_TrnQcApproval_Action"
            CHECK ("Action" IN ('','APPROVE','REQUEST_REVISION','REJECT'));
    END IF;
END $$;

DROP INDEX IF EXISTS "IX_TrnQcApproval_InspectionId_Level";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_TrnQcApproval_InspectionId_Level_Revision"
    ON "TrnQcApproval" ("InspectionId", "Level", "Revision");

-- =========================================================
-- 4. TrnQcFinding — lifecycle OPEN -> IN_PROGRESS -> DONE -> VERIFIED
--    VERIFIED = terminal (tidak ada lagi CLOSED terpisah).
-- =========================================================
UPDATE "TrnQcFinding" SET "Status" = 'IN_PROGRESS' WHERE "Status" = 'FOLLOW_UP';
UPDATE "TrnQcFinding" SET "Status" = 'DONE' WHERE "Status" = 'WAITING_VERIFICATION';
UPDATE "TrnQcFinding" SET "Status" = 'VERIFIED' WHERE "Status" = 'CLOSED';

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_TrnQcFinding_Status') THEN
        ALTER TABLE "TrnQcFinding" ADD CONSTRAINT "CK_TrnQcFinding_Status"
            CHECK ("Status" IN ('OPEN','IN_PROGRESS','DONE','VERIFIED'));
    END IF;
END $$;

-- =========================================================
-- 5. View daftar inspeksi & antrean approval (read-only, angka mentah)
-- =========================================================
CREATE OR REPLACE VIEW public."VwQcInspectionList" AS
SELECT
  i."Id", i."InspectionNo", i."FormId", f."Code" AS "FormCode", f."Name" AS "FormName",
  f."Version" AS "FormVersion", f."Pattern" AS "FormPattern",
  i."InspectionDate", i."CompanyId", i."CompanyNameSnapshot", i."WilayahKode",
  i."PersilSumber", i."PersilId", i."PersilKode", i."KodeAfdeling", i."Commodity",
  i."PeriodMonth", i."PeriodYear", i."Status", i."CurrentApprovalLevel", i."Revision",
  i."PemborongId", p."Name" AS "PemborongName",
  i."MandorId", m."Name" AS "MandorName",
  i."InspectorUserId", coalesce(i."InspectorNameSnapshot", iu."Name") AS "InspectorName",
  i."NoIcarLama",
  i."CreatedBy", cu."Name" AS "CreatedByName",
  i."CreatedAt", i."SubmittedAt", i."ApprovedAt", i."ClosedAt",
  coalesce(x."ActiveFindingCount", 0) AS "ActiveFindingCount",
  coalesce(x."OverdueFindingCount", 0) AS "OverdueFindingCount",
  s."Label" AS "CurrentStepLabel", s."JabatanKode" AS "CurrentStepJabatan"
FROM public."TrnQcInspection" i
JOIN public."MstQcForm" f ON f."Id" = i."FormId"
LEFT JOIN public."MstQcPemborong" p ON p."Id" = i."PemborongId"
LEFT JOIN public."MstQcMandor" m ON m."Id" = i."MandorId"
LEFT JOIN public."UtlUser" iu ON iu."Id" = i."InspectorUserId"
LEFT JOIN public."UtlUser" cu ON cu."UserId" = i."CreatedBy"
LEFT JOIN public."MstQcFormApprovalStep" s
  ON s."FormId" = i."FormId" AND s."Level" = i."CurrentApprovalLevel" AND s."Active"
LEFT JOIN (
  SELECT
    t."InspectionId",
    count(*) FILTER (WHERE t."Status" IN ('OPEN','IN_PROGRESS','DONE')) AS "ActiveFindingCount",
    count(*) FILTER (
      WHERE t."Status" IN ('OPEN','IN_PROGRESS','DONE')
        AND t."TargetDate" IS NOT NULL
        AND t."TargetDate" < (current_timestamp AT TIME ZONE 'Asia/Jakarta')::date
    ) AS "OverdueFindingCount"
  FROM public."TrnQcFinding" t
  GROUP BY t."InspectionId"
) x ON x."InspectionId" = i."Id";

CREATE OR REPLACE VIEW public."VwQcApprovalPending" AS
SELECT
  i."Id" AS "InspectionId", i."InspectionNo", i."FormId", f."Code" AS "FormCode", f."Name" AS "FormName",
  i."InspectionDate", i."CompanyNameSnapshot", i."WilayahKode", i."PersilKode", i."KodeAfdeling",
  i."Commodity", i."Status", i."CurrentApprovalLevel", i."Revision", i."SubmittedAt",
  i."CreatedBy", cu."Name" AS "CreatedByName",
  a."Id" AS "ApprovalId", a."CreatedAt" AS "PendingSince",
  s."Label" AS "StepLabel", s."JabatanKode" AS "StepJabatan",
  s."MenuCode" AS "StepMenuCode", s."ApproverGroupId" AS "StepApproverGroupId",
  g."Code" AS "StepApproverGroupCode", s."ScopeByAfdeling",
  (SELECT max(h."CreatedAt") FROM public."HistoryQcInspection" h WHERE h."InspectionId" = i."Id") AS "LastActionAt"
FROM public."TrnQcInspection" i
JOIN public."MstQcForm" f ON f."Id" = i."FormId"
JOIN public."TrnQcApproval" a
  ON a."InspectionId" = i."Id" AND a."Level" = i."CurrentApprovalLevel"
 AND a."Revision" = i."Revision" AND a."Status" = 'PENDING'
LEFT JOIN public."MstQcFormApprovalStep" s
  ON s."FormId" = i."FormId" AND s."Level" = i."CurrentApprovalLevel" AND s."Active"
LEFT JOIN public."UtlGroup" g ON g."Id" = s."ApproverGroupId"
LEFT JOIN public."UtlUser" cu ON cu."UserId" = i."CreatedBy"
WHERE i."Status" IN ('SUBMITTED','IN_REVIEW');

-- =========================================================
-- 6. View monitoring Fase 1 — selaras dengan status baru
--    (CREATE OR REPLACE: kolom lama tetap, kolom baru di akhir.)
-- =========================================================
CREATE OR REPLACE VIEW public."VwQcInspectionMonitoring" AS
SELECT
  i."Id", i."InspectionNo", i."FormId", f."Code" AS "FormCode", f."Name" AS "FormName",
  i."InspectionDate", i."CompanyId", i."CompanyNameSnapshot", i."WilayahKode",
  i."PersilSumber", i."PersilId", i."PersilKode", i."KodeAfdeling", i."Commodity",
  i."PeriodMonth", i."PeriodYear", i."Status", i."CurrentApprovalLevel",
  coalesce(x."OpenFindingCount", 0) AS "OpenFindingCount",
  coalesce(x."OverdueFindingCount", 0) AS "OverdueFindingCount",
  i."CreatedBy", i."CreatedAt", i."SubmittedAt", i."ApprovedAt", i."ClosedAt",
  i."Revision", i."PemborongId", i."MandorId", i."InspectorUserId", i."NoIcarLama"
FROM public."TrnQcInspection" i
JOIN public."MstQcForm" f ON f."Id" = i."FormId"
LEFT JOIN (
  SELECT
    t."InspectionId",
    count(*) FILTER (WHERE t."Status" IN ('OPEN','IN_PROGRESS','DONE')) AS "OpenFindingCount",
    count(*) FILTER (
      WHERE t."Status" IN ('OPEN','IN_PROGRESS','DONE')
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
  (f."Status" IN ('OPEN','IN_PROGRESS','DONE')
    AND f."TargetDate" IS NOT NULL
    AND f."TargetDate" < (current_timestamp AT TIME ZONE 'Asia/Jakarta')::date) AS "IsOverdue",
  f."FollowUpNote", f."VerifiedByUserId", f."VerifiedDate", f."CreatedAt", f."UpdatedAt"
FROM public."TrnQcFinding" f
JOIN public."TrnQcInspection" i ON i."Id" = f."InspectionId"
LEFT JOIN public."UtlUser" u ON u."Id" = f."PicUserId";

-- Verifikasi:
--   SELECT "Status", count(*) FROM "TrnQcInspection" GROUP BY "Status";
--   SELECT "Status", count(*) FROM "TrnQcFinding" GROUP BY "Status";
--   SELECT * FROM "VwQcApprovalPending";
--   SELECT conname FROM pg_constraint WHERE conname LIKE 'CK_TrnQc%' OR conname LIKE 'FK_TrnQcInspection_%';