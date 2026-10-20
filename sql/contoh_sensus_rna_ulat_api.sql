-- =====================================================================
-- CONTOH: mengisi form sensus RNA "Sensus Ulat Api" (HPT kelapa)
-- Database HelpDesk (PostgreSQL). Untuk uji coba / data contoh.
--
-- Bagian 1 = master (jenis sensus + parameter form). Aman dijalankan ulang.
-- Bagian 2 = satu kiriman sensus dari lapangan (seperti dikirim Android).
--
-- SEBELUM MENJALANKAN bagian 2, ganti nilai di blok "ISIAN":
--   v_user    = UserId petugas (kolom "UserId" di UtlUser)
--   v_persil  = persil asli dari MyPAD tblMstPersilKelapa (PersilID, KodeSubPersil,
--               KodeWilayah, KodeAfdeling). Lewat SQL tidak ada pengecekan persil,
--               jadi pastikan datanya benar.
-- =====================================================================


-- ---------------------------------------------------------------------
-- BAGIAN 1: MASTER JENIS SENSUS + PARAMETER
-- ---------------------------------------------------------------------

INSERT INTO "MstHpt" ("Code", "Name", "Kategori", "Komoditas", "MenuCode", "Description", "Active", "CreatedAt")
SELECT 'ULAT_API', 'Sensus Ulat Api', 'HPT', 'KELAPA', 'LYN_RNA_HPT_ULAT_API',
       'Sensus hama ulat api pada pokok kelapa.', true, now()
WHERE NOT EXISTS (SELECT 1 FROM "MstHpt" WHERE "Code" = 'ULAT_API');

-- TipeData: NUMBER, TEXT, BOOLEAN, OPTION.
-- IsIndikator + ambang: rata-rata nilai >= AmbangBerat -> BERAT, >= AmbangSedang -> SEDANG,
-- > 0 -> RINGAN, 0 -> NIHIL. Indikator utama analisis = parameter indikator dengan Id terkecil.
INSERT INTO "MstParameterHpt"
    ("JenisId", "Code", "Name", "Satuan", "TipeData", "Opsi", "Wajib", "IsIndikator", "AmbangSedang", "AmbangBerat", "Active")
SELECT j."Id", p.code, p.name, p.satuan, p.tipe, p.opsi, p.wajib, p.indikator, p.sedang, p.berat, true
FROM "MstHpt" j
CROSS JOIN (VALUES
    ('JML_ULAT',  'Jumlah ulat per pelepah', 'ekor', 'NUMBER', NULL,                 true,  true,  5::numeric, 10::numeric),
    ('FASE',      'Fase dominan',            NULL,   'OPTION', 'Larva,Kepompong,Ngengat', true,  false, NULL, NULL),
    ('KERUSAKAN', 'Kerusakan daun',          '%',    'NUMBER', NULL,                 false, false, NULL, NULL),
    ('KET',       'Keterangan',              NULL,   'TEXT',   NULL,                 false, false, NULL, NULL)
) AS p(code, name, satuan, tipe, opsi, wajib, indikator, sedang, berat)
WHERE j."Code" = 'ULAT_API'
  AND NOT EXISTS (SELECT 1 FROM "MstParameterHpt" x WHERE x."JenisId" = j."Id" AND x."Code" = p.code);


-- ---------------------------------------------------------------------
-- BAGIAN 2: SATU KIRIMAN SENSUS DARI LAPANGAN
-- 3 pokok sampel, 2 terserang. Nilai per pokok di SnsDetail ("NoPokok").
-- ---------------------------------------------------------------------

DO $$
DECLARE
    -- ===== ISIAN (ganti sesuai data asli) =====
    v_user          text    := 'admin';          -- UtlUser."UserId" petugas
    v_persil_id     text    := '12345';          -- MyPAD PersilID
    v_persil_kode   text    := 'KLP-A01-001';    -- MyPAD KodeSubPersil
    v_wilayah       text    := 'W01';            -- MyPAD KodeWilayah
    v_afdeling      text    := 'A01';            -- MyPAD KodeAfdeling
    v_tanggal       date    := current_date;     -- tidak boleh di masa depan
    -- ==========================================

    v_user_id   int;
    v_jenis_id  int;
    v_header_id bigint;
BEGIN
    SELECT "Id" INTO v_user_id FROM "UtlUser" WHERE "UserId" = v_user;
    IF v_user_id IS NULL THEN RAISE EXCEPTION 'User % tidak ditemukan', v_user; END IF;

    SELECT "Id" INTO v_jenis_id FROM "MstHpt" WHERE "Code" = 'ULAT_API';

    -- Header: satu baris per kiriman sensus.
    INSERT INTO "SnsHeader"
        ("ClientUuid", "JenisId", "PersilSumber", "PersilId", "PersilKode", "KodeWilayah", "KodeAfdeling",
         "Komoditas", "TanggalSensus", "PetugasUserId", "JumlahPokokSampel", "JumlahPokokTerserang",
         "Latitude", "Longitude", "Catatan", "DeviceInfo", "Status", "SubmittedAt", "CreatedAt")
    VALUES
        (gen_random_uuid(), v_jenis_id, 'KLP', v_persil_id, v_persil_kode, v_wilayah, v_afdeling,
         'KELAPA', v_tanggal, v_user_id, 3, 2,
         -0.512345, 101.456789, 'Contoh data sensus RNA (SQL).', 'manual-sql', 'SUBMITTED', now(), now())
    RETURNING "Id" INTO v_header_id;

    -- Nomor sensus sama seperti yang dibuat API: SNS-yyMMdd-000123 (waktu WIB).
    UPDATE "SnsHeader"
    SET "SensusNo" = 'SNS-' || to_char(now() AT TIME ZONE 'Asia/Jakarta', 'YYMMDD') || '-' || lpad(v_header_id::text, 6, '0')
    WHERE "Id" = v_header_id;

    -- Detail: nilai tiap parameter per pokok.
    INSERT INTO "SnsDetail" ("HeaderId", "ParameterId", "NoPokok", "NilaiAngka", "NilaiTeks")
    SELECT v_header_id, p."Id", d.no_pokok, d.angka, d.teks
    FROM (VALUES
        ('JML_ULAT',  1, 12::numeric, NULL),
        ('FASE',      1, NULL,        'Larva'),
        ('KERUSAKAN', 1, 30::numeric, NULL),
        ('JML_ULAT',  2, 4::numeric,  NULL),
        ('FASE',      2, NULL,        'Larva'),
        ('KERUSAKAN', 2, 10::numeric, NULL),
        ('JML_ULAT',  3, 0::numeric,  NULL),
        ('FASE',      3, NULL,        'Ngengat'),
        ('KET',       3, NULL,        'Pokok sehat')
    ) AS d(code, no_pokok, angka, teks)
    JOIN "MstParameterHpt" p ON p."JenisId" = v_jenis_id AND p."Code" = d.code;

    -- Foto (opsional).
    INSERT INTO "SnsFoto" ("HeaderId", "FileUrl", "Keterangan", "Latitude", "Longitude", "TakenAt")
    VALUES (v_header_id, '/uploads/sensus/contoh-ulat-api.jpg', 'Pokok 1', -0.512345, 101.456789, now());

    -- Riwayat.
    INSERT INTO "SnsLog" ("HeaderId", "UserId", "Action", "FromStatus", "ToStatus", "Note", "CreatedAt")
    VALUES (v_header_id, v_user_id, 'SUBMIT', NULL, 'SUBMITTED', 'Contoh data dari SQL.', now());

    RAISE NOTICE 'Sensus contoh dibuat, Id = %', v_header_id;
END $$;


-- ---------------------------------------------------------------------
-- CEK HASIL
-- ---------------------------------------------------------------------
-- SELECT h."SensusNo", h."PersilKode", h."Status", p."Name", d."NoPokok", d."NilaiAngka", d."NilaiTeks"
-- FROM "SnsHeader" h
-- JOIN "SnsDetail" d ON d."HeaderId" = h."Id"
-- JOIN "MstParameterHpt" p ON p."Id" = d."ParameterId"
-- WHERE h."DeviceInfo" = 'manual-sql'
-- ORDER BY h."Id" DESC, d."NoPokok", p."Id";

-- HAPUS DATA CONTOH (detail/foto ikut terhapus bila FK cascade; kalau tidak, hapus manual dulu):
-- DELETE FROM "SnsLog"    WHERE "HeaderId" IN (SELECT "Id" FROM "SnsHeader" WHERE "DeviceInfo" = 'manual-sql');
-- DELETE FROM "SnsDetail" WHERE "HeaderId" IN (SELECT "Id" FROM "SnsHeader" WHERE "DeviceInfo" = 'manual-sql');
-- DELETE FROM "SnsFoto"   WHERE "HeaderId" IN (SELECT "Id" FROM "SnsHeader" WHERE "DeviceInfo" = 'manual-sql');
-- DELETE FROM "SnsHeader" WHERE "DeviceInfo" = 'manual-sql';
