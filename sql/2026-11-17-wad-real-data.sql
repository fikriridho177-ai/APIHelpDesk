-- WAD real data — master bangunan air, log overflow, subsiden, dan blangko lapangan.
-- Target: PostgreSQL helpdeskasd. Idempotent, aman dijalankan ulang.
-- Sumber riil: paket "1. Helpdesk ASD" (2026_Laporan Buka-Tutup Overflow.xlsx,
-- Blangko Overflow.xlsx, September_Subsiden 2026.xlsx, Blangko Sipon.xlsx,
-- Lahan Tergenang.xlsx). Tidak ada data demo pada tabel ini.

BEGIN;

CREATE TABLE IF NOT EXISTS mst_wad_bangunan_air (
    id                bigserial PRIMARY KEY,
    kode_aset         varchar(40)  NOT NULL UNIQUE,
    wilayah           varchar(40)  NOT NULL,
    kanal             varchar(150) NOT NULL,
    nama_bangunan     varchar(200) NOT NULL,
    ukuran_dimensi    varchar(80),
    jumlah_papan      smallint,
    jenis_bangunan    varchar(40)  NOT NULL DEFAULT 'OVERFLOW',
    ref_tmak_lokasi   varchar(150),
    ref_tpmat_lokasi  varchar(150),
    aktif             boolean      NOT NULL DEFAULT true,
    kondisi_terakhir  varchar(250),
    sort_order        integer      NOT NULL DEFAULT 0,
    created_at        timestamptz  NOT NULL DEFAULT now(),
    updated_at        timestamptz
);

CREATE INDEX IF NOT EXISTS ix_mst_wad_bangunan_air_wilayah
    ON mst_wad_bangunan_air (wilayah, sort_order);

CREATE TABLE IF NOT EXISTS trx_wad_log_overflow (
    id               bigserial PRIMARY KEY,
    kode_aset        varchar(40) NOT NULL REFERENCES mst_wad_bangunan_air (kode_aset) ON DELETE CASCADE,
    minggu           varchar(5)  NOT NULL,
    tanggal_log      date        NOT NULL,
    status_operasi   varchar(10) NOT NULL,
    detail_bukaan    varchar(250),
    kondisi_fisik    varchar(250),
    petugas_nama     varchar(150),
    pemeriksa_nama   varchar(150),
    request_id       uuid        NOT NULL DEFAULT gen_random_uuid(),
    created_at       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_trx_wad_log_overflow_status CHECK (status_operasi IN ('Buka', 'Tutup')),
    CONSTRAINT uq_trx_wad_log_overflow_week UNIQUE (kode_aset, minggu, tanggal_log),
    CONSTRAINT uq_trx_wad_log_overflow_request UNIQUE (request_id)
);

CREATE INDEX IF NOT EXISTS ix_trx_wad_log_overflow_latest
    ON trx_wad_log_overflow (kode_aset, tanggal_log DESC, id DESC);

CREATE TABLE IF NOT EXISTS trx_wad_log_overflow_foto (
    id          bigserial PRIMARY KEY,
    log_id      bigint NOT NULL REFERENCES trx_wad_log_overflow (id) ON DELETE CASCADE,
    file_url    varchar(500) NOT NULL,
    keterangan  varchar(200),
    created_at  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_trx_wad_log_overflow_foto_log
    ON trx_wad_log_overflow_foto (log_id);

CREATE TABLE IF NOT EXISTS trx_wad_subsiden (
    id                bigserial PRIMARY KEY,
    kode_patok        varchar(60)  NOT NULL,
    lintang_raw       varchar(40),
    lintang           numeric(10,6),
    bujur_raw         varchar(40),
    bujur             numeric(10,6),
    wilayah           varchar(40)  NOT NULL,
    blok              varchar(60)  NOT NULL,
    tahun_tanam       integer,
    kedalaman_gambut  varchar(30),
    periode           varchar(10)  NOT NULL,
    periode_label     varchar(40)  NOT NULL,
    nilai_cm          numeric(10,2) NOT NULL,
    created_at        timestamptz  NOT NULL DEFAULT now(),
    CONSTRAINT uq_trx_wad_subsiden_periode UNIQUE (kode_patok, periode)
);

CREATE INDEX IF NOT EXISTS ix_trx_wad_subsiden_patok
    ON trx_wad_subsiden (kode_patok, periode);

CREATE TABLE IF NOT EXISTS trx_wad_subsiden_foto (
    id          bigserial PRIMARY KEY,
    kode_patok  varchar(60) NOT NULL,
    periode     varchar(10) NOT NULL,
    file_url    varchar(500) NOT NULL,
    keterangan  varchar(200),
    sort_order  smallint NOT NULL DEFAULT 0,
    created_at  timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT fk_trx_wad_subsiden_foto_obs FOREIGN KEY (kode_patok, periode)
        REFERENCES trx_wad_subsiden (kode_patok, periode) ON DELETE CASCADE
);

CREATE UNIQUE INDEX IF NOT EXISTS uq_trx_wad_subsiden_foto_slot
    ON trx_wad_subsiden_foto (kode_patok, periode, sort_order);

CREATE TABLE IF NOT EXISTS trx_wad_sipon (
    id               bigserial PRIMARY KEY,
    persil           varchar(80) NOT NULL,
    lintang          numeric(10,6),
    bujur            numeric(10,6),
    tanggal          date        NOT NULL,
    kondisi          varchar(4)  NOT NULL,
    tma_inlet_cm     numeric(8,2),
    tma_outlet_cm    numeric(8,2),
    tanggul_inlet    varchar(4),
    tanggul_outlet   varchar(4),
    komponen         jsonb       NOT NULL DEFAULT '[]'::jsonb,
    keterangan       varchar(1000),
    created_by       varchar(150) NOT NULL,
    created_at       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_trx_wad_sipon_kondisi CHECK (kondisi IN ('B', 'KB', 'RB'))
);

CREATE TABLE IF NOT EXISTS trx_wad_lahan_tergenang (
    id           bigserial PRIMARY KEY,
    persil       varchar(80) NOT NULL,
    lintang      numeric(10,6),
    bujur        numeric(10,6),
    tanggal      date        NOT NULL,
    kondisi      varchar(4)  NOT NULL,
    tmat_cm      numeric(8,2),
    luas_ha      numeric(10,4),
    akses_jalan  varchar(4),
    tanggul      varchar(4),
    keterangan   varchar(1000),
    created_by   varchar(150) NOT NULL,
    created_at   timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_trx_wad_lahan_kondisi CHECK (kondisi IN ('LP', 'T')),
    CONSTRAINT ck_trx_wad_lahan_akses CHECK (akses_jalan IS NULL OR akses_jalan IN ('B', 'S')),
    CONSTRAINT ck_trx_wad_lahan_tanggul CHECK (tanggul IS NULL OR tanggul IN ('B', 'J'))
);

COMMIT;
