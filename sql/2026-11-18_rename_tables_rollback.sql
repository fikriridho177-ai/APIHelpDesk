-- Rollback rename tabel domain HelpDesk-ASD.
-- Jalankan hanya bersama rollback kode yang sudah memakai nama baru.
-- Dikecualikan: seluruh objek WAD dan IoT.
BEGIN;
SET LOCAL lock_timeout = '10s';
SELECT pg_advisory_xact_lock(20261118, 2);

DROP VIEW IF EXISTS public."VwSnsMonitoring";
DROP VIEW IF EXISTS public."VwSnsDetailMonitoring";
DROP VIEW IF EXISTS public."VwRnaRingkasan";
DROP VIEW IF EXISTS public."VwQcInspectionMonitoring";
DROP VIEW IF EXISTS public."VwQcFindingMonitoring";
DROP VIEW IF EXISTS public."VwLynRequestMonitoring";

DO $$
DECLARE
  r record;
  kind "char";
BEGIN
  FOR r IN
    SELECT * FROM (VALUES
      ('UtlChatGroup',            'ChatGroup'),
      ('UtlChatRole',             'ChatRole'),
      ('UtlChatMember',           'ChatMember'),
      ('TrnChatMessage',          'ChatMessage'),
      ('TrnLynRequest',           'LynRequest'),
      ('MstLynJenisPermintaan',   'LynJenisPermintaan'),
      ('HistoryLynRequestLog',    'LynRequestLog'),
      ('MstSnsKebun',             'SnsKebun'),
      ('MstSnsAfdeling',          'SnsAfdeling'),
      ('MstSnsBlok',              'SnsBlok'),
      ('MstHptParameter',         'MstParameterHpt'),
      ('TrnSnsHeader',            'SnsHeader'),
      ('TrnSnsDetail',            'SnsDetail'),
      ('TrnSnsFoto',              'SnsFoto'),
      ('HistorySnsLog',           'SnsLog'),
      ('MstQcForm',               'QcForm'),
      ('TrnQcInspection',         'QcInspection'),
      ('TrnQcApproval',           'QcApproval'),
      ('TrnQcFinding',            'QcFinding'),
      ('HistoryQcInspection',     'QcHistory')
    ) AS v(new_name, old_name)
  LOOP
    SELECT c.relkind INTO kind
    FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = 'public' AND c.relname = r.new_name
      AND c.relkind IN ('r', 'p', 'v', 'm');

    IF kind IS NULL THEN
      IF EXISTS (
        SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public' AND c.relname = r.old_name
          AND c.relkind IN ('r', 'p', 'v', 'm')
      ) THEN
        RAISE NOTICE 'Lewati %: sudah memakai nama %', r.new_name, r.old_name;
      ELSE
        RAISE NOTICE 'Lewati %: tidak ditemukan', r.new_name;
      END IF;
      CONTINUE;
    END IF;

    IF EXISTS (
      SELECT 1 FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
      WHERE n.nspname = 'public' AND c.relname = r.old_name
        AND c.relkind IN ('r', 'p', 'v', 'm')
    ) THEN
      RAISE EXCEPTION 'Rollback dibatalkan: % dan % sama-sama ada', r.new_name, r.old_name;
    END IF;

    CASE
      WHEN kind IN ('r', 'p') THEN
        EXECUTE format('ALTER TABLE public.%I RENAME TO %I', r.new_name, r.old_name);
      WHEN kind = 'v' THEN
        EXECUTE format('ALTER VIEW public.%I RENAME TO %I', r.new_name, r.old_name);
      WHEN kind = 'm' THEN
        EXECUTE format('ALTER MATERIALIZED VIEW public.%I RENAME TO %I', r.new_name, r.old_name);
      ELSE
        RAISE EXCEPTION 'Objek % bertipe % dan tidak dapat di-rename otomatis', r.new_name, kind;
    END CASE;
  END LOOP;
END $$;

-- Kembalikan view kompatibilitas yang dipakai kode lama.
CREATE OR REPLACE VIEW public."MstHptParameter" AS
SELECT * FROM public."MstParameterHpt";

COMMIT;
