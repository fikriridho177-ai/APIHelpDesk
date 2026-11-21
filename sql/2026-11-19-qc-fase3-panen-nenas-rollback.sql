-- QC Module — Rollback Fase 3: Panen Nenas
-- Mengembalikan kondisi sebelum 2026-11-19-qc-fase3-panen-nenas.sql:
--   * drop view vwQcPanenNenasDetail & vwQcPanenNenasRekap
--   * drop tabel TrnQcInspSampelPanenNenas (HANYA bila kosong)
--   * nonaktifkan kembali form QC-PANEN-NNS
-- Guard: bila tabel detail sudah berisi baris, rollback DIBATALKAN
-- (tidak men-drop data tanpa konfirmasi; migrasikan dulu secara manual).
-- Inspeksi QC-PANEN-NNS yang sudah ada tetap sah (FK form tidak diubah);
-- hanya form-nya yang kembali tidak aktif untuk pembuatan baru.

BEGIN;

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM "TrnQcInspSampelPanenNenas" LIMIT 1) THEN
        RAISE EXCEPTION 'Rollback Fase 3 dibatalkan: TrnQcInspSampelPanenNenas sudah berisi data. Migrasikan/arsipkan dulu, lalu jalankan ulang.';
    END IF;
END $$;

DROP VIEW IF EXISTS "vwQcPanenNenasRekap";
DROP VIEW IF EXISTS "vwQcPanenNenasDetail";
DROP TABLE IF EXISTS "TrnQcInspSampelPanenNenas";

UPDATE "MstQcForm" SET "Active" = false WHERE "Code" = 'QC-PANEN-NNS';

COMMIT;