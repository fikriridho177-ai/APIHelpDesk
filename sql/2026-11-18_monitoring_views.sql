-- View monitoring/analisis read-only untuk domain Sensus/RNA, QC, dan Layanan.
-- Tidak mencakup WAD maupun IoT. Jalankan setelah rename tabel.
BEGIN;
SELECT pg_advisory_xact_lock(20261118, 3);

CREATE OR REPLACE VIEW public."VwSnsMonitoring" AS
SELECT
  h."Id", h."SensusNo", h."ClientUuid", h."JenisId", j."Name" AS "JenisName",
  h."PersilSumber", h."PersilId", h."PersilKode", h."KodeWilayah", h."KodeAfdeling", h."Komoditas",
  h."BlokId", b."Code" AS "BlokKode", b."Name" AS "BlokName",
  a."Id" AS "AfdelingId", a."Code" AS "AfdelingKode", a."Name" AS "AfdelingName",
  k."Id" AS "KebunId", k."Code" AS "KebunKode", k."Name" AS "KebunName",
  h."TanggalSensus", h."PetugasUserId", u."Name" AS "PetugasName",
  h."JumlahPokokSampel", h."JumlahPokokTerserang",
  CASE
    WHEN h."JumlahPokokSampel" > 0
      THEN round((h."JumlahPokokTerserang"::numeric * 100) / h."JumlahPokokSampel", 2)
  END AS "PersenTerserang",
  h."JenisForm", h."StatusLahan", h."StatusLahanSumber", h."FaseTanaman", h."LuasHa", h."JumlahPokokPersil",
  h."Status", h."SubmittedAt", h."ApprovedByUserId", h."ApprovedAt", h."ApprovalNote",
  h."CreatedAt", h."UpdatedAt"
FROM public."TrnSnsHeader" h
JOIN public."MstHpt" j ON j."Id" = h."JenisId"
LEFT JOIN public."MstSnsBlok" b ON b."Id" = h."BlokId"
LEFT JOIN public."MstSnsAfdeling" a ON a."Id" = b."AfdelingId"
LEFT JOIN public."MstSnsKebun" k ON k."Id" = a."KebunId"
LEFT JOIN public."UtlUser" u ON u."Id" = h."PetugasUserId";

CREATE OR REPLACE VIEW public."VwSnsDetailMonitoring" AS
SELECT
  d."Id", d."HeaderId", h."SensusNo", h."TanggalSensus", h."JenisId", j."Name" AS "JenisName",
  h."PersilSumber", h."PersilId", h."PersilKode", h."KodeWilayah", h."JenisForm", h."Status",
  d."ParameterId", p."Code" AS "ParameterCode", p."Name" AS "ParameterName", p."Satuan" AS "ParameterUnit",
  d."NoPokok", d."NilaiAngka", d."NilaiTeks"
FROM public."TrnSnsDetail" d
JOIN public."TrnSnsHeader" h ON h."Id" = d."HeaderId"
JOIN public."MstHpt" j ON j."Id" = h."JenisId"
JOIN public."MstHptParameter" p ON p."Id" = d."ParameterId";

CREATE OR REPLACE VIEW public."VwRnaRingkasan" AS
SELECT
  date_trunc('month', h."TanggalSensus")::date AS "Periode",
  h."JenisId", j."Name" AS "JenisName", h."KodeWilayah",
  count(*) AS "TotalSensus",
  count(*) FILTER (WHERE h."Status" = 'SUBMITTED') AS "Submitted",
  count(*) FILTER (WHERE h."Status" = 'APPROVED') AS "Approved",
  count(*) FILTER (WHERE h."Status" = 'REVISI') AS "Revisi",
  count(*) FILTER (WHERE h."Status" = 'REJECTED') AS "Rejected",
  coalesce(sum(h."JumlahPokokSampel"), 0) AS "TotalPokokSampel",
  coalesce(sum(h."JumlahPokokTerserang"), 0) AS "TotalPokokTerserang"
FROM public."TrnSnsHeader" h
JOIN public."MstHpt" j ON j."Id" = h."JenisId"
GROUP BY 1, 2, 3, 4;

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
    f."InspectionId",
    count(*) FILTER (WHERE f."Status" <> 'CLOSED') AS "OpenFindingCount",
    count(*) FILTER (
      WHERE f."Status" <> 'CLOSED'
        AND f."TargetDate" IS NOT NULL
        AND f."TargetDate" < (current_timestamp AT TIME ZONE 'Asia/Jakarta')::date
    ) AS "OverdueFindingCount"
  FROM public."TrnQcFinding" f
  GROUP BY f."InspectionId"
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

CREATE OR REPLACE VIEW public."VwLynRequestMonitoring" AS
SELECT
  r."Id", r."RequestNo", r."MenuId", m."Name" AS "MenuName",
  r."JenisId", j."Name" AS "JenisName",
  r."RequesterUserId", coalesce(r."RequesterName", u."Name") AS "RequesterDisplayName",
  u."Name" AS "RequesterUserName", u."Email" AS "RequesterUserEmail",
  r."RequesterDeptId", r."RequesterCompanyId", r."RequesterDepartment", r."RequesterCompanies",
  r."Title", r."Location", r."NeededDate", r."Priority", r."Status",
  r."ScheduledAt", r."Assignee", r."CreatedAt", r."UpdatedAt", r."ClosedAt",
  l."LastLogAt", coalesce(l."LogCount", 0) AS "LogCount"
FROM public."TrnLynRequest" r
JOIN public."UtlMenu" m ON m."Id" = r."MenuId"
LEFT JOIN public."MstLynJenisPermintaan" j ON j."Id" = r."JenisId"
LEFT JOIN public."UtlUser" u ON u."Id" = r."RequesterUserId"
LEFT JOIN (
  SELECT l."RequestId", max(l."CreatedAt") AS "LastLogAt", count(*) AS "LogCount"
  FROM public."HistoryLynRequestLog" l
  GROUP BY l."RequestId"
) l ON l."RequestId" = r."Id";

COMMIT;
