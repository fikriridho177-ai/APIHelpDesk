-- QC Module — Fase 1: Fondasi & Master
-- Target database : helpdeskasd (PostgreSQL). Jalankan MANUAL setelah review.
-- Idempotent: ADD COLUMN IF NOT EXISTS, CREATE TABLE/INDEX IF NOT EXISTS,
--             INSERT ... ON CONFLICT DO NOTHING, UPDATE hanya bila metadata masih kosong.
-- Script ini TANPA BEGIN/COMMIT internal supaya bisa dibungkus transaksi uji
-- (apply committed, re-run idempotensi, dan uji DB kosong via ROLLBACK).
-- Tidak men-drop kolom/tabel berisi data. Index unik lama Code diganti (Code, Version)
-- karena satu Code kini boleh punya beberapa versi.
-- Rollback: 2026-11-19-qc-foundation-rollback.sql

-- =========================================================
-- 1. MstQcForm — versi, pola, template, judul cetak, kode Excel
-- =========================================================
ALTER TABLE "MstQcForm" ADD COLUMN IF NOT EXISTS "Version"       int          NOT NULL DEFAULT 1;
ALTER TABLE "MstQcForm" ADD COLUMN IF NOT EXISTS "EffectiveFrom" date         NULL;
ALTER TABLE "MstQcForm" ADD COLUMN IF NOT EXISTS "Pattern"       varchar(2)   NULL;
ALTER TABLE "MstQcForm" ADD COLUMN IF NOT EXISTS "TemplateFile"  varchar(200) NULL;
ALTER TABLE "MstQcForm" ADD COLUMN IF NOT EXISTS "PrintTitle"    varchar(300) NULL;
-- Kode form asli pada file Excel (FRM-QC-...). NULL bila file contoh tidak berkode
-- atau kodenya bentrok (menunggu konfirmasi, lihat docs/qc/01-foundation.md).
ALTER TABLE "MstQcForm" ADD COLUMN IF NOT EXISTS "ExcelCode"     varchar(64)  NULL;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_MstQcForm_Pattern') THEN
        ALTER TABLE "MstQcForm" ADD CONSTRAINT "CK_MstQcForm_Pattern"
            CHECK ("Pattern" IS NULL OR "Pattern" IN ('A','B','C','D','E'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_MstQcForm_Version') THEN
        ALTER TABLE "MstQcForm" ADD CONSTRAINT "CK_MstQcForm_Version" CHECK ("Version" >= 1);
    END IF;
END $$;

DROP INDEX IF EXISTS "IX_MstQcForm_Code";
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcForm_Code_Version" ON "MstQcForm" ("Code", "Version");
CREATE INDEX IF NOT EXISTS "IX_MstQcForm_ExcelCode" ON "MstQcForm" ("ExcelCode");

-- Metadata form pilot yang sudah ada (hanya bila masih kosong, tidak menimpa edit user).
UPDATE "MstQcForm" SET
    "Pattern" = 'A', "ExcelCode" = 'FRM-QC-K-019-01',
    "TemplateFile" = '19-panen-kelapa.xlsx', "PrintTitle" = "Name",
    "EffectiveFrom" = DATE '2025-01-01'
WHERE "Code" = 'QC-PANEN-KLP' AND "Pattern" IS NULL;

UPDATE "MstQcForm" SET
    "Pattern" = 'B', "ExcelCode" = 'FRM-QC-N-006-00',
    "TemplateFile" = '06-pupuk-tabur-nns.xlsx', "PrintTitle" = "Name",
    "EffectiveFrom" = DATE '2025-01-01'
WHERE "Code" = 'QC-PUPUK-TABUR-NNS' AND "Pattern" IS NULL;

UPDATE "MstQcForm" SET
    "Pattern" = 'E', "ExcelCode" = NULL,
    "TemplateFile" = 'kondisi-kebersihan-kebun-nanas.xlsx', "PrintTitle" = "Name",
    "EffectiveFrom" = DATE '2025-01-01'
WHERE "Code" = 'QC-KEBERSIHAN-NNS' AND "Pattern" IS NULL;

-- Form pilot yang belum ada di database live + form baru sesuai peta 11 file Excel.
-- Form baru ber-Active=false: UI inputnya dibangun bertahap pada Fase 3/5/6,
-- sehingga "Buat Inspeksi" tidak menampilkan form tanpa grid.
INSERT INTO "MstQcForm"
    ("Code", "Name", "Commodity", "Description", "Active", "Version", "EffectiveFrom",
     "Pattern", "TemplateFile", "PrintTitle", "ExcelCode", "CreatedBy", "CreatedAt")
VALUES
    ('QC-TEBAS-SANITASI', 'Tebas dan Sanitasi', 'KELAPA',
     'Pemeriksaan tebas manual/mekanis dan sanitasi kelapa (file 37, varian CONTOH 2016 & TEBAS TOTAL+SANITASI 2025).',
     true, 1, DATE '2025-01-01', 'A', '37-tebas-sanitasi.xlsx', 'Tebas dan Sanitasi', NULL, 'system', now()),
    ('QC-INVENTARIS-KLP', 'Inventaris Kelapa', 'KELAPA',
     'Pengecekan inventaris tanaman kelapa per jalur (file 31).',
     true, 1, DATE '2025-01-01', 'C', '31-inventaris-kelapa.xlsx', 'Inventaris Kelapa', 'FRM-QC-011-00', 'system', now()),
    ('QC-PL-NNS', 'PL Nenas', 'NANAS',
     'Pemeriksaan persiapan lahan nanas (file 01, sheet PL-AWAL).',
     false, 1, DATE '2025-01-01', 'A', '01-pl-nenas.xlsx', 'PL Nenas', 'FRM-QC-N-001-00', 'system', now()),
    ('QC-HORMON-NNS', 'Hormon Nenas', 'NANAS',
     'Pemeriksaan aplikasi hormon nanas (file 09).',
     false, 1, DATE '2025-01-01', 'A', '09-hormon.xlsx', 'Hormon Nenas', 'FRM-QC-N-009-00', 'system', now()),
    ('QC-PANEN-NNS', 'Panen Nenas', 'NANAS',
     'Pemeriksaan panen nanas per jalur sampel (file 12, sheet PNN NNS). Form percontohan Fase 3.',
     false, 1, DATE '2025-01-01', 'A', '12-panen-nenas.xlsx', 'Panen Nenas', 'FRM-QC-N-012-00', 'system', now()),
    ('QC-PEMUPUKAN-KLP', 'Pemupukan Kelapa', 'KELAPA',
     'Pemeriksaan pemupukan kelapa per jalur sampel (file 17, acuan 2026).',
     false, 1, DATE '2026-01-01', 'A', '17-pemupukan-kelapa.xlsx', 'Pemupukan Kelapa', 'FRM-QC-K-017-00', 'system', now()),
    ('QC-PUPUK-POWER-NNS', 'Pupuk Power Nenas', 'NANAS',
     'Pemeriksaan dosis pupuk power/kocor nanas per jenis pupuk (file 07).',
     false, 1, DATE '2025-01-01', 'B', '07-pupuk-power-nns.xlsx', 'Pupuk Power Nenas', 'FRM-QC-N-007-00', 'system', now()),
    ('QC-PUPUK-DASAR-NNS', 'Pupuk Dasar (RP) Nenas', 'NANAS',
     'Pemeriksaan pupuk dasar RP nanas (file 06 sheet PPK DASAR). Kode Excel bentrok (FRM-QC-N-02-00 / FRM-QC-N-06-00) menunggu konfirmasi.',
     false, 1, DATE '2025-01-01', 'B', '06-pupuk-tabur-nns.xlsx', 'Pupuk Dasar (RP) Nenas', NULL, 'system', now()),
    ('QC-PENGIRIMAN-NNS', 'Monitoring Pengiriman Nenas', 'NANAS',
     'Monitoring pengiriman buah nanas per persil & grade N1-N5 (file 04, tanpa kode form).',
     false, 1, DATE '2025-01-01', 'D', '04-monitoring-pengiriman-nenas.xlsx', 'Monitoring Pengiriman Nenas', NULL, 'system', now()),
    ('QC-PANEN-KLP-RSTM', 'Panen Kelapa (RSTM)', 'KELAPA',
     'Varian RSTM file 19 (FRM-QC-K-019-00): kolom & footer berbeda dengan REV-1. Pemisahan per PT menunggu konfirmasi.',
     false, 1, DATE '2025-01-01', 'A', '19-panen-kelapa.xlsx', 'Panen Kelapa (RSTM)', 'FRM-QC-K-019-00', 'system', now())
ON CONFLICT ("Code", "Version") DO NOTHING;

-- =========================================================
-- 2. MstQcFormApprovalStep — alur approval per form (master, bukan hardcode)
--    Level 1 "Dilaporkan" = pembuat dokumen (baris otomatis saat submit, Fase 2).
--    ApproverGroupId NULL selama belum ada group QC yang cocok; sementara hak
--    approve tetap lewat MenuCode (pola QcAccess.CanApproveOn yang sudah ada).
-- =========================================================
CREATE TABLE IF NOT EXISTS "MstQcFormApprovalStep" (
    "Id"               serial       PRIMARY KEY,
    "FormId"           int          NOT NULL,
    "Level"            int          NOT NULL,
    "Label"            varchar(100) NOT NULL,
    "JabatanKode"      varchar(64)  NULL,
    "ApproverGroupId"  int          NULL,
    "MenuCode"         varchar(64)  NOT NULL DEFAULT 'QC_APPROVAL',
    "ScopeByAfdeling"  boolean      NOT NULL DEFAULT false,
    "Active"           boolean      NOT NULL DEFAULT true,
    "CreatedBy"        varchar(100) NULL,
    "CreatedAt"        timestamptz  NOT NULL DEFAULT now(),
    "UpdatedBy"        varchar(100) NULL,
    "UpdatedAt"        timestamptz  NULL,
    CONSTRAINT "FK_MstQcFormApprovalStep_MstQcForm"
        FOREIGN KEY ("FormId") REFERENCES "MstQcForm" ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_MstQcFormApprovalStep_UtlGroup"
        FOREIGN KEY ("ApproverGroupId") REFERENCES "UtlGroup" ("Id") ON DELETE SET NULL
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_MstQcFormApprovalStep_Level') THEN
        ALTER TABLE "MstQcFormApprovalStep" ADD CONSTRAINT "CK_MstQcFormApprovalStep_Level"
            CHECK ("Level" BETWEEN 1 AND 8);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcFormApprovalStep_FormId_Level"
    ON "MstQcFormApprovalStep" ("FormId", "Level");

INSERT INTO "MstQcFormApprovalStep" ("FormId", "Level", "Label", "JabatanKode", "ScopeByAfdeling", "CreatedBy", "CreatedAt")
SELECT f."Id", x."Level", x."Label", x."JabatanKode", x."ScopeByAfdeling", 'system', now()
FROM "MstQcForm" f
CROSS JOIN (VALUES
    (1, 'Dilaporkan',   'INSPEKTUR_QC',     false),
    (2, 'P. Jawab Afd', 'ASISTEN_WILAYAH',  true),
    (3, 'Diperiksa',    'ASST_QC',          false),
    (4, 'Disetujui',    'SNR_MGR_QC',       false)
) AS x("Level", "Label", "JabatanKode", "ScopeByAfdeling")
ON CONFLICT ("FormId", "Level") DO NOTHING;

-- =========================================================
-- 3. MstQcPersil — cache master persil dari SQL Server (read-only source)
--    Di-upsert oleh QcPersilSyncService (manual + terjadwal). Angka mentah saja;
--    umur tanam & SPH dihitung di view pada fase terkait.
-- =========================================================
CREATE TABLE IF NOT EXISTS "MstQcPersil" (
    "Id"             serial        PRIMARY KEY,
    "Sumber"         varchar(20)   NOT NULL,   -- KLP / TNM_NNS / LHN_NNS / PNN
    "PersilId"       varchar(100)  NOT NULL,   -- PersilID sumber
    "PersilKode"     varchar(100)  NOT NULL,   -- KodeSubPersil / Kode sumber
    "NamaPersil"     varchar(200)  NULL,
    "Komoditas"      varchar(32)   NOT NULL,
    "WilayahKode"    varchar(50)   NULL,
    "KodeDivisi"     varchar(50)   NULL,
    "KodeAfdeling"   varchar(50)   NULL,
    "Kcb"            varchar(50)   NULL,
    "BulanTanam"     int           NULL,
    "TahunTanam"     int           NULL,
    "LuasHa"         numeric(12,3) NULL,
    "JumlahJalur"    int           NULL,
    "JumlahPohon"    int           NULL,
    "PemborongKode"  varchar(64)   NULL,
    "PemborongNama"  varchar(150)  NULL,
    "Active"         boolean       NOT NULL DEFAULT true,
    "SyncedAt"       timestamptz   NOT NULL DEFAULT now(),
    "CreatedAt"      timestamptz   NOT NULL DEFAULT now(),
    "UpdatedAt"      timestamptz   NULL
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_MstQcPersil_BulanTanam') THEN
        ALTER TABLE "MstQcPersil" ADD CONSTRAINT "CK_MstQcPersil_BulanTanam"
            CHECK ("BulanTanam" IS NULL OR "BulanTanam" BETWEEN 1 AND 12);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcPersil_Sumber_PersilId" ON "MstQcPersil" ("Sumber", "PersilId");
CREATE INDEX IF NOT EXISTS "IX_MstQcPersil_WilayahKode_KodeAfdeling" ON "MstQcPersil" ("WilayahKode", "KodeAfdeling");
CREATE INDEX IF NOT EXISTS "IX_MstQcPersil_Komoditas_Active" ON "MstQcPersil" ("Komoditas", "Active");
CREATE INDEX IF NOT EXISTS "IX_MstQcPersil_PersilKode" ON "MstQcPersil" ("PersilKode");

-- =========================================================
-- 4. MstQcPemborong — master pemborong (sebagian tersync dari sumber PNN/KLP)
-- =========================================================
CREATE TABLE IF NOT EXISTS "MstQcPemborong" (
    "Id"          serial       PRIMARY KEY,
    "Kode"        varchar(64)  NOT NULL,
    "Name"        varchar(150) NOT NULL,
    "WilayahKode" varchar(50)  NULL,
    "Sumber"      varchar(20)  NOT NULL DEFAULT 'LOCAL',  -- LOCAL / SYNC
    "Active"      boolean      NOT NULL DEFAULT true,
    "CreatedBy"   varchar(100) NULL,
    "CreatedAt"   timestamptz  NOT NULL DEFAULT now(),
    "UpdatedBy"   varchar(100) NULL,
    "UpdatedAt"   timestamptz  NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcPemborong_Kode" ON "MstQcPemborong" ("Kode");

-- =========================================================
-- 5. MstQcMandor — master mandor (tidak ada di sumber, CRUD lokal)
-- =========================================================
CREATE TABLE IF NOT EXISTS "MstQcMandor" (
    "Id"           serial       PRIMARY KEY,
    "Name"         varchar(150) NOT NULL,
    "WilayahKode"  varchar(50)  NULL,
    "KodeAfdeling" varchar(50)  NULL,
    "PemborongId"  int          NULL,
    "Active"       boolean      NOT NULL DEFAULT true,
    "CreatedBy"    varchar(100) NULL,
    "CreatedAt"    timestamptz  NOT NULL DEFAULT now(),
    "UpdatedBy"    varchar(100) NULL,
    "UpdatedAt"    timestamptz  NULL,
    CONSTRAINT "FK_MstQcMandor_MstQcPemborong"
        FOREIGN KEY ("PemborongId") REFERENCES "MstQcPemborong" ("Id") ON DELETE SET NULL
);
CREATE INDEX IF NOT EXISTS "IX_MstQcMandor_KodeAfdeling" ON "MstQcMandor" ("KodeAfdeling");

-- =========================================================
-- 6. MstQcJenisPupuk — master jenis pupuk (pola B)
-- =========================================================
CREATE TABLE IF NOT EXISTS "MstQcJenisPupuk" (
    "Id"              serial        PRIMARY KEY,
    "Kode"            varchar(32)   NOT NULL,
    "Name"            varchar(100)  NOT NULL,
    "Satuan"          varchar(20)   NOT NULL DEFAULT 'gram',
    "DosisJadwalGram" numeric(12,2) NULL,
    "Komoditas"       varchar(32)   NULL,
    "Active"          boolean       NOT NULL DEFAULT true,
    "CreatedBy"       varchar(100)  NULL,
    "CreatedAt"       timestamptz   NOT NULL DEFAULT now(),
    "UpdatedBy"       varchar(100)  NULL,
    "UpdatedAt"       timestamptz   NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcJenisPupuk_Kode" ON "MstQcJenisPupuk" ("Kode");

-- Jenis pupuk sesuai sheet PPK POWER file 07. Dosis jadwal kosong (menunggu standar).
INSERT INTO "MstQcJenisPupuk" ("Kode", "Name", "Satuan", "Komoditas", "CreatedBy", "CreatedAt")
VALUES
    ('UREA',   'Urea',   'gram', 'NANAS', 'system', now()),
    ('MOP',    'MOP',    'gram', 'NANAS', 'system', now()),
    ('CUSO4',  'CuSO4',  'gram', 'NANAS', 'system', now()),
    ('CAO',    'CaO',    'gram', 'NANAS', 'system', now()),
    ('FESO4',  'FeSO4',  'gram', 'NANAS', 'system', now()),
    ('ZNSO4',  'ZnSO4',  'gram', 'NANAS', 'system', now()),
    ('RP',     'RP',     'gram', 'NANAS', 'system', now()),
    ('ETHREL', 'Ethrel', 'gram', 'NANAS', 'system', now())
ON CONFLICT ("Kode") DO NOTHING;

-- =========================================================
-- 7. MstQcGrade — master grade (kebersihan & pengiriman)
-- =========================================================
CREATE TABLE IF NOT EXISTS "MstQcGrade" (
    "Id"        serial       PRIMARY KEY,
    "Kode"      varchar(32)  NOT NULL,
    "Name"      varchar(100) NOT NULL,
    "Komoditas" varchar(32)  NULL,
    "Urutan"    int          NOT NULL DEFAULT 0,
    "Active"    boolean      NOT NULL DEFAULT true,
    "CreatedBy" varchar(100) NULL,
    "CreatedAt" timestamptz  NOT NULL DEFAULT now(),
    "UpdatedBy" varchar(100) NULL,
    "UpdatedAt" timestamptz  NULL
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcGrade_Kode" ON "MstQcGrade" ("Kode");

-- Kode grade literal dari file contoh (kebersihan: H / AK-AM / K-M;
-- pengiriman: N1-N5 dengan sub NA/NB/NC). Arti label menunggu konfirmasi kebun.
INSERT INTO "MstQcGrade" ("Kode", "Name", "Komoditas", "Urutan", "CreatedBy", "CreatedAt")
VALUES
    ('H',     'H',     'NANAS', 1,  'system', now()),
    ('AK-AM', 'AK-AM', 'NANAS', 2,  'system', now()),
    ('K-M',   'K-M',   'NANAS', 3,  'system', now()),
    ('N1',    'N1',    'NANAS', 4,  'system', now()),
    ('N2',    'N2',    'NANAS', 5,  'system', now()),
    ('N3',    'N3',    'NANAS', 6,  'system', now()),
    ('N4',    'N4',    'NANAS', 7,  'system', now()),
    ('N5',    'N5',    'NANAS', 8,  'system', now()),
    ('NA',    'NA',    'NANAS', 9,  'system', now()),
    ('NB',    'NB',    'NANAS', 10, 'system', now()),
    ('NC',    'NC',    'NANAS', 11, 'system', now())
ON CONFLICT ("Kode") DO NOTHING;

-- =========================================================
-- 8. MstQcStandard — target % per form per parameter (validasi & temuan otomatis)
--    Belum di-seed: angka target menunggu konfirmasi (pertanyaan recon #6).
-- =========================================================
CREATE TABLE IF NOT EXISTS "MstQcStandard" (
    "Id"            serial        PRIMARY KEY,
    "FormId"        int           NOT NULL,
    "ParameterKey"  varchar(64)   NOT NULL,
    "TargetPercent" numeric(6,2)  NOT NULL,
    "Operator"      varchar(4)    NOT NULL DEFAULT 'MAX',
    "Keterangan"    varchar(300)  NULL,
    "Active"        boolean       NOT NULL DEFAULT true,
    "CreatedBy"     varchar(100)  NULL,
    "CreatedAt"     timestamptz   NOT NULL DEFAULT now(),
    "UpdatedBy"     varchar(100)  NULL,
    "UpdatedAt"     timestamptz   NULL,
    CONSTRAINT "FK_MstQcStandard_MstQcForm"
        FOREIGN KEY ("FormId") REFERENCES "MstQcForm" ("Id") ON DELETE RESTRICT
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_MstQcStandard_Operator') THEN
        ALTER TABLE "MstQcStandard" ADD CONSTRAINT "CK_MstQcStandard_Operator"
            CHECK ("Operator" IN ('MAX','MIN'));
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'CK_MstQcStandard_TargetPercent') THEN
        ALTER TABLE "MstQcStandard" ADD CONSTRAINT "CK_MstQcStandard_TargetPercent"
            CHECK ("TargetPercent" BETWEEN 0 AND 100);
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcStandard_FormId_ParameterKey"
    ON "MstQcStandard" ("FormId", "ParameterKey");

-- =========================================================
-- 9. MstQcNumberSeq — penomoran atomik (dipakai Fase 2)
-- =========================================================
CREATE TABLE IF NOT EXISTS "MstQcNumberSeq" (
    "Id"        serial       PRIMARY KEY,
    "ScopeKey"  varchar(120) NOT NULL,
    "LastValue" int          NOT NULL DEFAULT 0,
    "UpdatedAt" timestamptz  NOT NULL DEFAULT now()
);
CREATE UNIQUE INDEX IF NOT EXISTS "IX_MstQcNumberSeq_ScopeKey" ON "MstQcNumberSeq" ("ScopeKey");

-- =========================================================
-- 10. Menu QC_MASTER + hak default group ADMIN
-- =========================================================
INSERT INTO "UtlMenu" ("ParentId", "Code", "Name", "Route", "Icon", "Description", "MenuType", "SortOrder", "Active", "CreatedBy", "CreatedAt")
SELECT parent."Id", 'QC_MASTER', 'Master QC', '/qc/master', 'settings',
       'Master form & versi, alur approval, cache persil, pemborong, mandor, jenis pupuk, grade, dan standar QC.',
       'MENU', 8, true, 'system', now()
FROM "UtlMenu" parent
WHERE parent."Code" = 'QC'
  AND NOT EXISTS (SELECT 1 FROM "UtlMenu" m WHERE m."Code" = 'QC_MASTER');

INSERT INTO "UtlGroupMenu"
    ("GroupId", "MenuId", "CanView", "CanRead", "CanCreate", "CanUpdate", "CanDelete", "CanApprove", "CanExport")
SELECT g."Id", m."Id", true, true, true, true, true, true, true
FROM "UtlGroup" g
CROSS JOIN "UtlMenu" m
WHERE g."Code" = 'ADMIN'
  AND m."Code" = 'QC_MASTER'
  AND NOT EXISTS (
      SELECT 1 FROM "UtlGroupMenu" gm
      WHERE gm."GroupId" = g."Id" AND gm."MenuId" = m."Id"
  );

-- Verifikasi:
--   SELECT "Code","Version","Pattern","ExcelCode","Active" FROM "MstQcForm" ORDER BY "Code","Version";
--   SELECT f."Code", s."Level", s."Label" FROM "MstQcFormApprovalStep" s
--     JOIN "MstQcForm" f ON f."Id" = s."FormId" ORDER BY f."Code", s."Level";
--   SELECT "Sumber", count(*) FROM "MstQcPersil" GROUP BY "Sumber";